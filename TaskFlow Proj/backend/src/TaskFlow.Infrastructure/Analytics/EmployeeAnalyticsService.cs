using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Rules;
using TaskFlow.Infrastructure.Persistence;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;
using static TaskFlow.Infrastructure.Analytics.AnalyticsCounting;

namespace TaskFlow.Infrastructure.Analytics;

public class EmployeeAnalyticsService : IEmployeeAnalyticsService
{
    /// <summary>A department with fewer active members than this shows only its task total, so a colleague's workload
    /// cannot be read off the aggregates.</summary>
    public const int MinDepartmentMembersForBreakdown = 3;

    private const int UpcomingListSize = 10;

    private readonly AppDbContext _dbContext;
    private readonly IEmployeeDashboardScope _scope;
    private readonly IAppClock _clock;

    public EmployeeAnalyticsService(AppDbContext dbContext, IEmployeeDashboardScope scope, IAppClock clock)
    {
        _dbContext = dbContext;
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

        var own = _scope.OwnTasks(_dbContext.Tasks.AsNoTracking(), userId);
        var byStatus = await TotalsAsync(own, now, cancellationToken);
        var trend = await TrendAsync(own, from, today, timeZone, cancellationToken);

        return new EmployeeAnalyticsResponseDto(
            selected,
            from,
            today,
            await DepartmentAsync(userId, now, cancellationToken),
            new EmployeeSummaryDto(byStatus.Total, byStatus.CompletionPercent, byStatus.Completed, byStatus.Open),
            byStatus,
            await ByPriorityAsync(own, cancellationToken),
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
        var departmentId = await _dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => user.DepartmentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (departmentId is null)
        {
            return null;
        }

        var name = await _dbContext.Departments
            .Where(department => department.Id == departmentId)
            .Select(department => department.Name)
            .SingleOrDefaultAsync(cancellationToken);
        if (name is null)
        {
            return null;
        }

        var members = _dbContext.Users.Where(user => user.DepartmentId == departmentId && user.IsActive);
        var tasks = _dbContext.Tasks.AsNoTracking()
            .Where(TaskBucketRules.IsCounted())
            .Where(task => members.Any(member => member.Id == task.AssignedToUserId));
        var total = await tasks.CountAsync(cancellationToken);

        if (await members.CountAsync(cancellationToken) < MinDepartmentMembersForBreakdown)
        {
            return new EmployeeDepartmentDto(name, true, total, null);
        }

        return new EmployeeDepartmentDto(name, false, total, await TotalsAsync(tasks, now, cancellationToken));
    }

    private static async Task<IReadOnlyList<EmployeeDeadlineDto>> UpcomingAsync(
        IQueryable<TaskEntity> own,
        DateTime now,
        DateOnly today,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var horizon = DeadlineBucketRules.BoundsFor(now, timeZone).HorizonUtc;
        var rows = await own
            .Where(task => task.Status != TaskStatus.Completed && task.Deadline < horizon)
            .OrderBy(task => task.Deadline)
            .ThenBy(task => task.Id)
            .Take(UpcomingListSize)
            .Select(task => new { task.Id, task.TaskCode, task.Title, task.Priority, task.Deadline })
            .ToListAsync(cancellationToken);

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
        IQueryable<TaskEntity> own,
        CancellationToken cancellationToken)
    {
        var perProject = await own
            .Where(task => task.ProjectId != null)
            .GroupBy(task => task.ProjectId!.Value)
            .Select(group => new
            {
                ProjectId = group.Key,
                Total = group.Count(),
                Completed = group.Count(task => task.Status == TaskStatus.Completed),
                Latest = group
                    .Where(task => task.Status != TaskStatus.Completed)
                    .Max(task => (DateTime?)task.Deadline)
            })
            .ToListAsync(cancellationToken);

        var membership = await _dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => user.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);
        var ids = perProject.Select(row => row.ProjectId).ToList();
        if (membership is int memberOf && !ids.Contains(memberOf))
        {
            ids.Add(memberOf);
        }

        var projects = await _dbContext.Projects
            .Where(project => ids.Contains(project.Id))
            .Select(project => new { project.Id, project.Name })
            .ToListAsync(cancellationToken);

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
                    row?.Latest);
            })
            .ToList();
    }

    private static async Task<IReadOnlyList<TrendPointDto>> TrendAsync(
        IQueryable<TaskEntity> own,
        DateOnly from,
        DateOnly to,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var fromUtc = ReminderCalculator.LocalMidnightUtc(from, timeZone);
        var toUtc = ReminderCalculator.LocalMidnightUtc(to.AddDays(1), timeZone);

        var created = await QuarterHourCountsAsync(
            own.Where(task => task.CreatedAt >= fromUtc && task.CreatedAt < toUtc).Select(task => task.CreatedAt),
            cancellationToken);
        var completed = await QuarterHourCountsAsync(
            own.Where(task => task.Status == TaskStatus.Completed
                    && task.CompletedAt != null
                    && task.CompletedAt >= fromUtc
                    && task.CompletedAt < toUtc)
                .Select(task => task.CompletedAt!.Value),
            cancellationToken);

        Dictionary<DateOnly, int> PerDay(List<QuarterHourCount> rows) => rows
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
