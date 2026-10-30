using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Application.Settings;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Persistence;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using static TaskFlow.Infrastructure.Analytics.AnalyticsCounting;

namespace TaskFlow.Infrastructure.Analytics;

public class ManagerAnalyticsService : IManagerAnalyticsService
{
    private const string NoProject = "No project";
    private const int NoKey = 0;

    private readonly AppDbContext _dbContext;
    private readonly IManagerDashboardScope _scope;
    private readonly IAppClock _clock;

    public ManagerAnalyticsService(AppDbContext dbContext, IManagerDashboardScope scope, IAppClock clock)
    {
        _dbContext = dbContext;
        _scope = scope;
        _clock = clock;
    }

    public async Task<ManagerAnalyticsResponseDto> GetAsync(
        int managerId,
        AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var scoped = await _scope.ScopeAsync(managerId, filter, cancellationToken);
        var resolved = scoped.Filter;
        var now = resolved.NowUtc;

        var team = await TeamAsync(managerId, cancellationToken);
        var byStatus = await TotalsAsync(scoped.Tasks, now, cancellationToken);
        var byProject = await ByProjectAsync(managerId, scoped.Tasks, resolved, cancellationToken);

        return new ManagerAnalyticsResponseDto(
            new AppliedFilterDto(
                resolved.From,
                resolved.To,
                resolved.Basis,
                null,
                resolved.ProjectId,
                resolved.EmployeeId,
                null),
            new TeamSummaryDto(
                resolved.EmployeeId is null ? team.Count : 1,
                resolved.ProjectId is null ? byProject.Count(project => project.ProjectId is not null) : 1,
                byStatus),
            byStatus,
            await ByPriorityAsync(scoped.Tasks, cancellationToken),
            await ByEmployeeAsync(managerId, filter, scoped, team, cancellationToken),
            byProject,
            await AttentionAsync(scoped.Tasks, byProject, resolved, cancellationToken));
    }

    public async Task<ManagerFilterOptionsDto> GetFilterOptionsAsync(int managerId, CancellationToken cancellationToken)
    {
        var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
        var today = ReminderCalculator.LocalDate(_clock.UtcNow, timeZone);

        var employees = await _dbContext.Users
            .Where(user => _scope.TeamUserIds(managerId).Contains(user.Id))
            .OrderBy(user => user.FullName)
            .Select(user => new AnalyticsOptionDto(user.Id, user.FullName))
            .ToListAsync(cancellationToken);
        var projectIds = _scope.ProjectIds(managerId);
        var projects = await _dbContext.Projects
            .Where(project => projectIds.Contains(project.Id))
            .OrderBy(project => project.Name)
            .Select(project => new AnalyticsOptionDto(project.Id, project.Name))
            .ToListAsync(cancellationToken);

        return new ManagerFilterOptionsDto(today, timeZone.Id, employees, projects);
    }

    private async Task<Dictionary<int, string>> TeamAsync(int managerId, CancellationToken cancellationToken) =>
        await _dbContext.Users
            .Where(user => _scope.TeamUserIds(managerId).Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

    private async Task<IReadOnlyList<TeamEmployeeDto>> ByEmployeeAsync(
        int managerId,
        AnalyticsFilter requested,
        ScopedTasks scoped,
        Dictionary<int, string> team,
        CancellationToken cancellationToken)
    {
        // The flags compare a member with the whole team, so an employee filter narrows the rows but not the baseline.
        var baseline = scoped.Tasks;
        if (requested.EmployeeId is not null)
        {
            // The dates are pinned to the first resolution so both queries cover the same range, and the bucket
            // boundary below uses the first run's "now", so a task at its deadline classifies identically in both.
            var wholeTeam = requested with { EmployeeId = null, From = scoped.Filter.From, To = scoped.Filter.To };
            baseline = (await _scope.ScopeAsync(managerId, wholeTeam, cancellationToken)).Tasks;
        }

        var recipients = _dbContext.TaskRecipients;
        var perMember = await CountByAsync(
            baseline,
            scoped.Filter.NowUtc,
            query =>
            {
                var assignees = query.Select(task => new { TaskId = task.Id, UserId = task.AssignedToUserId });
                var shared = query.SelectMany(task => recipients
                    .Where(recipient => recipient.TaskId == task.Id)
                    .Select(recipient => new { TaskId = recipient.TaskId, UserId = recipient.UserId }));
                return assignees
                    .Union(shared)
                    .GroupBy(pair => pair.UserId)
                    .Select(group => new KeyCount(group.Key, group.Count()));
            },
            cancellationToken);

        var counts = team.Keys.ToDictionary(id => id, id => perMember.GetValueOrDefault(id, Empty));
        var mean = counts.Count == 0 ? 0 : counts.Values.Average(bucket => bucket.Open);

        return counts
            .Where(pair => requested.EmployeeId is null || pair.Key == requested.EmployeeId)
            .Select(pair => new TeamEmployeeDto(
                pair.Key,
                team[pair.Key],
                pair.Value,
                ManagerTeamThresholds.IsHighWorkload(pair.Value.Open, mean),
                ManagerTeamThresholds.HasOverdue(pair.Value.Overdue)))
            .OrderByDescending(item => item.Tasks.Open)
            .ThenByDescending(item => item.Tasks.Total)
            .ThenBy(item => item.Name)
            .ToList();
    }

    private async Task<IReadOnlyList<ProjectAnalyticsDto>> ByProjectAsync(
        int managerId,
        IQueryable<TaskEntity> tasks,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var perProject = await CountByAsync(
            tasks,
            filter.NowUtc,
            query => query
                .GroupBy(task => task.ProjectId ?? NoKey)
                .Select(group => new KeyCount(group.Key, group.Count())),
            cancellationToken);

        var latestDeadlines = (await tasks
                .GroupBy(task => task.ProjectId ?? NoKey)
                .Select(group => new { group.Key, Latest = group.Max(task => task.Deadline) })
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.Key, row => row.Latest);

        var inScope = _scope.ProjectIds(managerId);
        var projects = _dbContext.Projects.Where(project => inScope.Contains(project.Id));
        if (filter.ProjectId is int onlyProject)
        {
            projects = projects.Where(project => project.Id == onlyProject);
        }

        var named = await projects
            .OrderBy(project => project.Name)
            .Select(project => new { project.Id, project.Name })
            .ToListAsync(cancellationToken);

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

    private async Task<TeamAttentionDto> AttentionAsync(
        IQueryable<TaskEntity> tasks,
        IReadOnlyList<ProjectAnalyticsDto> byProject,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var overdue = await tasks
            .Where(TaskBucketRules.InBucket(TaskBucket.Overdue, filter.NowUtc))
            .OrderBy(task => task.Deadline)
            .ThenBy(task => task.Id)
            .Take(ManagerTeamThresholds.AttentionListSize)
            .Select(task => new
            {
                task.Id,
                task.TaskCode,
                task.Title,
                task.Deadline,
                task.AssignedToUserId,
                task.ProjectId
            })
            .ToListAsync(cancellationToken);

        var userIds = overdue.Select(task => task.AssignedToUserId).Distinct().ToList();
        var names = await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
        var projectIds = overdue.Where(task => task.ProjectId != null).Select(task => task.ProjectId!.Value).Distinct().ToList();
        var projectNames = await _dbContext.Projects
            .IgnoreQueryFilters()
            .Where(project => projectIds.Contains(project.Id))
            .Select(project => new { project.Id, project.Name })
            .ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken);

        var delayed = overdue
            .Select(task => new DelayedTaskDto(
                task.Id,
                task.TaskCode,
                task.Title,
                names.GetValueOrDefault(task.AssignedToUserId, "Unknown"),
                task.ProjectId is int projectId ? projectNames.GetValueOrDefault(projectId) : null,
                DaysOverdue(task.Deadline, filter)))
            .ToList();

        var attentionProjects = byProject
            .Where(project => project.ProjectId is not null && project.Tasks.Overdue > 0)
            .OrderByDescending(project => project.Tasks.Overdue)
            .ThenBy(project => project.Name)
            .Take(ManagerTeamThresholds.AttentionListSize)
            .Select(project => new ProjectAttentionDto(project.ProjectId!.Value, project.Name, project.Tasks.Overdue))
            .ToList();

        return new TeamAttentionDto(delayed, attentionProjects);
    }

    /// <summary>Whole local days between the deadline's day and today, never negative.</summary>
    private static int DaysOverdue(DateTime deadlineUtc, ResolvedAnalyticsFilter filter) =>
        Math.Max(0, filter.Today.DayNumber - ReminderCalculator.LocalDate(deadlineUtc, filter.TimeZone).DayNumber);
}
