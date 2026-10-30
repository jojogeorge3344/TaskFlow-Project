using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Repositories.Interfaces.Dashboard.Analytics;
using TaskFlow.Api.Services.Interfaces.Dashboard.Analytics;
using TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings;
using static TaskFlow.Api.Services.Dashboard.Analytics.AnalyticsBuckets;

namespace TaskFlow.Api.Services.Dashboard.Analytics;

public class AdminAnalyticsService : IAdminAnalyticsService
{
    private const string NoDepartment = "No department";
    private const string NoProject = "No project";
    private const int TopEmployees = 10;
    private const int DailyTrendMaxDays = 31;

    private readonly IAnalyticsRepository _analytics;
    private readonly IDashboardScope _scope;
    private readonly IAppClock _clock;

    public AdminAnalyticsService(IAnalyticsRepository analytics, IDashboardScope scope, IAppClock clock)
    {
        _analytics = analytics;
        _scope = scope;
        _clock = clock;
    }

    public async Task<AdminAnalyticsDto> GetAsync(AnalyticsFilter filter, CancellationToken cancellationToken)
    {
        var scoped = await _scope.ScopeAsync(filter, cancellationToken);
        var resolved = scoped.Filter;
        var now = resolved.NowUtc;

        var byStatus = await _analytics.CountByStatusAsync(scoped.Tasks, now, cancellationToken);

        return new AdminAnalyticsDto(
            new AppliedFilterDto(
                resolved.From,
                resolved.To,
                resolved.Basis,
                resolved.DepartmentId,
                resolved.ProjectId,
                resolved.EmployeeId,
                resolved.ManagerId),
            await SummaryAsync(resolved, byStatus, cancellationToken),
            byStatus,
            await _analytics.CountByPriorityAsync(scoped.Tasks, cancellationToken),
            await _analytics.CountByDeadlineAsync(
                scoped.Tasks,
                DeadlineBucketRules.BoundsFor(resolved.NowUtc, resolved.TimeZone),
                cancellationToken),
            await TrendAsync(scoped.TasksIgnoringDates, resolved, cancellationToken),
            await ByDepartmentAsync(scoped.Tasks, resolved, cancellationToken),
            await ByEmployeeAsync(scoped.Tasks, now, resolved.AllEmployees, cancellationToken),
            await ByManagerAsync(scoped.Tasks, resolved, cancellationToken),
            await ByProjectAsync(scoped.Tasks, resolved, cancellationToken));
    }

    public async Task<AnalyticsFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
        var today = ReminderCalculator.LocalDate(_clock.UtcNow, timeZone);

        return new AnalyticsFilterOptionsDto(
            today,
            timeZone.Id,
            await _analytics.GetDepartmentsAsync(null, cancellationToken),
            await _analytics.GetProjectsAsync(null, null, cancellationToken),
            await _analytics.GetUsersAsync(cancellationToken),
            await _analytics.GetManagersAsync(null, cancellationToken));
    }

    private async Task<AnalyticsSummaryDto> SummaryAsync(
        ResolvedAnalyticsFilter filter,
        BucketCountsDto byStatus,
        CancellationToken cancellationToken) =>
        new(
            await _analytics.CountEmployeesAsync(
                PermissionKeys.TasksViewAll,
                filter.EmployeeId,
                filter.DepartmentId,
                filter.ManagerId,
                cancellationToken),
            await _analytics.CountManagersAsync(filter.ManagerId, cancellationToken),
            await _analytics.CountDepartmentsAsync(filter.DepartmentId, cancellationToken),
            await _analytics.CountProjectsAsync(filter.ProjectId, cancellationToken),
            byStatus);

    private async Task<TrendDto> TrendAsync(
        AnalyticsTaskSet tasks,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var created = await _analytics.CountCreatedPerQuarterHourAsync(
            tasks, filter.FromUtc, filter.ToUtcExclusive, cancellationToken);
        var completed = await _analytics.CountCompletedPerQuarterHourAsync(
            tasks, filter.FromUtc, filter.ToUtcExclusive, completedStatusOnly: false, cancellationToken);

        var weekly = filter.To.DayNumber - filter.From.DayNumber + 1 > DailyTrendMaxDays;
        var firstBucket = weekly ? StartOfWeek(filter.From) : filter.From;
        var step = weekly ? 7 : 1;

        DateOnly BucketOf(DateTime utc)
        {
            var day = ReminderCalculator.LocalDate(utc, filter.TimeZone);
            return weekly ? StartOfWeek(day) : day;
        }

        var createdPerBucket = created.GroupBy(row => BucketOf(row.Start)).ToDictionary(group => group.Key, group => group.Sum(row => row.Count));
        var completedPerBucket = completed.GroupBy(row => BucketOf(row.Start)).ToDictionary(group => group.Key, group => group.Sum(row => row.Count));

        var points = new List<TrendPointDto>();
        for (var day = firstBucket; day <= filter.To; day = day.AddDays(step))
        {
            points.Add(new TrendPointDto(
                day,
                createdPerBucket.GetValueOrDefault(day),
                completedPerBucket.GetValueOrDefault(day)));
        }

        return new TrendDto(weekly ? "week" : "day", points);
    }

    private static DateOnly StartOfWeek(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    private async Task<IReadOnlyList<DepartmentAnalyticsDto>> ByDepartmentAsync(
        AnalyticsTaskSet tasks,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var perDepartment = await _analytics.CountByDepartmentAsync(tasks, filter.NowUtc, cancellationToken);
        var named = await _analytics.GetDepartmentsAsync(filter.DepartmentId, cancellationToken);

        var result = named
            .Select(department => new DepartmentAnalyticsDto(
                department.Id,
                department.Name,
                perDepartment.GetValueOrDefault(department.Id, Empty)))
            .ToList();

        var knownIds = named.Select(department => department.Id).ToHashSet();
        var unassigned = Sum(perDepartment.Where(pair => !knownIds.Contains(pair.Key)).Select(pair => pair.Value));
        if (unassigned.Total > 0)
        {
            result.Add(new DepartmentAnalyticsDto(null, NoDepartment, unassigned));
        }

        return result;
    }

    private async Task<EmployeeAnalyticsSectionDto> ByEmployeeAsync(
        AnalyticsTaskSet tasks,
        DateTime now,
        bool all,
        CancellationToken cancellationToken)
    {
        var perEmployee = await _analytics.CountByPersonAsync(tasks, now, cancellationToken);

        var top = perEmployee
            .OrderByDescending(pair => pair.Value.Open)
            .ThenByDescending(pair => pair.Value.Total)
            .ThenBy(pair => pair.Key)
            .Take(all ? int.MaxValue : TopEmployees)
            .ToList();

        var names = await _analytics.GetUserNamesIncludingDeletedAsync(
            top.Select(pair => pair.Key).ToList(),
            cancellationToken);

        var items = top
            .Select(pair => new EmployeeAnalyticsDto(pair.Key, names.GetValueOrDefault(pair.Key, "Unknown"), pair.Value))
            .OrderByDescending(item => item.Tasks.Open)
            .ThenByDescending(item => item.Tasks.Total)
            .ThenBy(item => item.Name)
            .ToList();

        return new EmployeeAnalyticsSectionDto(items, perEmployee.Count);
    }

    private async Task<IReadOnlyList<ManagerAnalyticsDto>> ByManagerAsync(
        AnalyticsTaskSet tasks,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var listed = await _analytics.GetManagersAsync(filter.ManagerId, cancellationToken);
        var teamTasks = await _analytics.CountByDepartmentManagerAsync(tasks, filter.NowUtc, cancellationToken);
        var teamSizes = await _analytics.GetDepartmentTeamSizesAsync(cancellationToken);
        var departmentCounts = await _analytics.GetManagedDepartmentCountsAsync(cancellationToken);
        var projectCounts = await _analytics.GetManagedProjectCountsAsync(cancellationToken);

        return listed
            .Select(manager => new ManagerAnalyticsDto(
                manager.Id,
                manager.Name,
                departmentCounts.GetValueOrDefault(manager.Id),
                teamSizes.GetValueOrDefault(manager.Id),
                projectCounts.GetValueOrDefault(manager.Id),
                teamTasks.GetValueOrDefault(manager.Id, Empty)))
            .ToList();
    }

    private async Task<IReadOnlyList<ProjectAnalyticsDto>> ByProjectAsync(
        AnalyticsTaskSet tasks,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var perProject = await _analytics.CountByProjectAsync(tasks, filter.NowUtc, cancellationToken);
        var latestDeadlines = await _analytics.GetLatestDeadlineByProjectAsync(tasks, cancellationToken);
        var named = await _analytics.GetProjectsAsync(filter.ProjectId, null, cancellationToken);

        var result = named
            .Select(project => new ProjectAnalyticsDto(
                project.Id,
                project.Name,
                perProject.GetValueOrDefault(project.Id, Empty),
                latestDeadlines.TryGetValue(project.Id, out var latest) ? latest : null))
            .ToList();

        var knownIds = named.Select(project => project.Id).ToHashSet();
        var unassigned = Sum(perProject.Where(pair => !knownIds.Contains(pair.Key)).Select(pair => pair.Value));
        if (unassigned.Total > 0)
        {
            var latest = latestDeadlines.Where(pair => !knownIds.Contains(pair.Key)).Select(pair => pair.Value).Max();
            result.Add(new ProjectAnalyticsDto(null, NoProject, unassigned, latest));
        }

        return result;
    }
}
