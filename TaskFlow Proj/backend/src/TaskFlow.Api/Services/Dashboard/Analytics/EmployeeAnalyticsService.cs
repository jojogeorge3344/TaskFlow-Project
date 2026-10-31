using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Repositories.Dashboard.Analytics;
using TaskFlow.Api.Services.Settings.ApplicationSettings;

namespace TaskFlow.Api.Services.Dashboard.Analytics;

public class EmployeeAnalyticsService : IEmployeeAnalyticsService
{
    /// <summary>A department with fewer active members than this shows no task figures at all, so a colleague's workload
    /// cannot be read off the aggregates.</summary>
    public const int MinDepartmentMembersForBreakdown = 3;

    private const int UpcomingListSize = 10;

    private readonly IAnalyticsRepository _analytics;
    private readonly IEmployeeDashboardScope _scope;
    private readonly IAppClock _clock;

    public EmployeeAnalyticsService(IAnalyticsRepository analytics, IEmployeeDashboardScope scope, IAppClock clock)
    {
        _analytics = analytics;
        _scope = scope;
        _clock = clock;
    }

    public async Task<EmployeeAnalyticsResponseDto> GetAsync(int userId, string? period, CancellationToken cancellationToken)
    {
        var selected = ParsePeriod(period);
        var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
        var now = _clock.UtcNow;
        var today = ReminderCalculator.LocalDate(now, timeZone);
        var from = selected switch
        {
            EmployeeAnalyticsPeriod.Last7Days => today.AddDays(-6),
            EmployeeAnalyticsPeriod.Last30Days => today.AddDays(-29),
            _ => new DateOnly(today.Year, today.Month, 1)
        };

        var own = _scope.OwnTasks(userId);
        var byStatus = await _analytics.CountByStatusAsync(own, now, cancellationToken);
        var trend = await TrendAsync(own, from, today, timeZone, cancellationToken);

        return new EmployeeAnalyticsResponseDto(
            selected,
            from,
            today,
            await DepartmentAsync(userId, now, cancellationToken),
            new EmployeeSummaryDto(byStatus.Total, byStatus.CompletionPercent, byStatus.Completed, byStatus.Open),
            byStatus,
            await _analytics.CountByPriorityAsync(own, cancellationToken),
            await UpcomingAsync(own, now, today, timeZone, cancellationToken),
            await ProjectsAsync(userId, own, cancellationToken),
            trend,
            trend.Sum(point => point.Completed));
    }

    private static EmployeeAnalyticsPeriod ParsePeriod(string? period) => period?.Trim().ToLowerInvariant() switch
    {
        null or "" or "30d" => EmployeeAnalyticsPeriod.Last30Days,
        "7d" => EmployeeAnalyticsPeriod.Last7Days,
        "month" => EmployeeAnalyticsPeriod.ThisMonth,
        _ => throw new AnalyticsFilterException("The period is not valid.")
    };

    private async Task<EmployeeDepartmentDto?> DepartmentAsync(int userId, DateTime now, CancellationToken cancellationToken)
    {
        // A person may belong to several departments; the card shows the first by name.
        if (await _analytics.GetFirstDepartmentIdAsync(userId, cancellationToken) is not int departmentId)
        {
            return null;
        }

        var name = await _analytics.GetDepartmentNameAsync(departmentId, cancellationToken);
        if (name is null)
        {
            return null;
        }

        if (await _analytics.CountActiveDepartmentMembersAsync(departmentId, cancellationToken) < MinDepartmentMembersForBreakdown)
        {
            return new EmployeeDepartmentDto(name, true, null, null);
        }

        var totals = await _analytics.CountByStatusAsync(
            new AnalyticsTaskSet(ActiveMembersOfDepartmentId: departmentId),
            now,
            cancellationToken);
        return new EmployeeDepartmentDto(name, false, totals.Total, totals);
    }

    private async Task<IReadOnlyList<EmployeeDeadlineDto>> UpcomingAsync(
        AnalyticsTaskSet own,
        DateTime now,
        DateOnly today,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var horizon = DeadlineBucketRules.BoundsFor(now, timeZone).HorizonUtc;
        var rows = await _analytics.GetOpenDueBeforeAsync(own, horizon, UpcomingListSize, cancellationToken);

        return rows
            .Select(task =>
            {
                var days = ReminderCalculator.LocalDate(task.Deadline, timeZone).DayNumber - today.DayNumber;
                var overdue = task.Deadline < now;
                return new EmployeeDeadlineDto(
                    task.Id,
                    task.TaskCode,
                    task.Title,
                    task.Priority.ToString(),
                    task.Deadline,
                    days,
                    overdue,
                    overdue ? Math.Max(0, -days) : 0);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<EmployeeProjectDto>> ProjectsAsync(
        int userId,
        AnalyticsTaskSet own,
        CancellationToken cancellationToken)
    {
        var perProject = await _analytics.GetProjectProgressAsync(own, cancellationToken);
        var memberships = await _analytics.GetAllocatedProjectIdsAsync(userId, cancellationToken);
        var ids = perProject.Select(row => row.ProjectId).ToList();
        ids.AddRange(memberships.Where(memberOf => !ids.Contains(memberOf)));

        var projects = await _analytics.GetProjectsByIdsAsync(ids, cancellationToken);

        return projects
            .OrderBy(project => project.Name)
            .Select(project =>
            {
                var row = perProject.FirstOrDefault(candidate => candidate.ProjectId == project.Id);
                var total = row?.Total ?? 0;
                var completed = row?.Completed ?? 0;
                return new EmployeeProjectDto(
                    project.Id,
                    project.Name,
                    total,
                    completed,
                    TaskBucketRules.CompletionPercent(completed, total),
                    row?.LatestOpenDeadline);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<TrendPointDto>> TrendAsync(
        AnalyticsTaskSet own,
        DateOnly from,
        DateOnly to,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var fromUtc = ReminderCalculator.LocalMidnightUtc(from, timeZone);
        var toUtc = ReminderCalculator.LocalMidnightUtc(to.AddDays(1), timeZone);

        var created = await _analytics.CountCreatedPerQuarterHourAsync(own, fromUtc, toUtc, cancellationToken);
        var completed = await _analytics.CountCompletedPerQuarterHourAsync(own, fromUtc, toUtc, completedStatusOnly: true, cancellationToken);

        Dictionary<DateOnly, int> PerDay(IReadOnlyList<QuarterHourCount> rows) => rows
            .GroupBy(row => ReminderCalculator.LocalDate(row.Start, timeZone))
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Count));

        var createdPerDay = PerDay(created);
        var completedPerDay = PerDay(completed);

        var points = new List<TrendPointDto>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            points.Add(new TrendPointDto(day, createdPerDay.GetValueOrDefault(day), completedPerDay.GetValueOrDefault(day)));
        }

        return points;
    }
}
