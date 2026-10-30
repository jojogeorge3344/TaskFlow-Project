using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Repositories.Interfaces.Dashboard.Analytics;
using TaskFlow.Api.Services.Interfaces.Dashboard.Analytics;
using TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings;
using static TaskFlow.Api.Services.Dashboard.Analytics.AnalyticsBuckets;

namespace TaskFlow.Api.Services.Dashboard.Analytics;

public class ManagerAnalyticsService : IManagerAnalyticsService
{
    private const string NoProject = "No project";

    private readonly IAnalyticsRepository _analytics;
    private readonly IManagerDashboardScope _scope;
    private readonly IAppClock _clock;

    public ManagerAnalyticsService(IAnalyticsRepository analytics, IManagerDashboardScope scope, IAppClock clock)
    {
        _analytics = analytics;
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
        var byStatus = await _analytics.CountByStatusAsync(scoped.Tasks, now, cancellationToken);
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
            await _analytics.CountByPriorityAsync(scoped.Tasks, cancellationToken),
            await ByEmployeeAsync(managerId, filter, scoped, team, cancellationToken),
            byProject,
            await AttentionAsync(scoped.Tasks, byProject, resolved, cancellationToken));
    }

    public async Task<ManagerFilterOptionsDto> GetFilterOptionsAsync(int managerId, CancellationToken cancellationToken)
    {
        var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
        var today = ReminderCalculator.LocalDate(_clock.UtcNow, timeZone);

        var employees = (await TeamAsync(managerId, cancellationToken))
            .OrderBy(member => member.Value, StringComparer.Ordinal)
            .Select(member => new AnalyticsOptionDto(member.Key, member.Value))
            .ToList();
        var projects = await _analytics.GetProjectsAsync(
            null,
            await _scope.ProjectIdsAsync(managerId, cancellationToken),
            cancellationToken);

        return new ManagerFilterOptionsDto(today, timeZone.Id, employees, projects);
    }

    private async Task<IReadOnlyDictionary<int, string>> TeamAsync(int managerId, CancellationToken cancellationToken) =>
        await _analytics.GetUserNamesAsync(await _scope.TeamUserIdsAsync(managerId, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<TeamEmployeeDto>> ByEmployeeAsync(
        int managerId,
        AnalyticsFilter requested,
        ScopedTasks scoped,
        IReadOnlyDictionary<int, string> team,
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

        var perMember = await _analytics.CountByPersonAsync(baseline, scoped.Filter.NowUtc, cancellationToken);

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
        AnalyticsTaskSet tasks,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var perProject = await _analytics.CountByProjectAsync(tasks, filter.NowUtc, cancellationToken);
        var latestDeadlines = await _analytics.GetLatestDeadlineByProjectAsync(tasks, cancellationToken);
        var named = await _analytics.GetProjectsAsync(
            filter.ProjectId,
            await _scope.ProjectIdsAsync(managerId, cancellationToken),
            cancellationToken);

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
        AnalyticsTaskSet tasks,
        IReadOnlyList<ProjectAnalyticsDto> byProject,
        ResolvedAnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var overdue = await _analytics.GetOverdueAsync(tasks, filter.NowUtc, ManagerTeamThresholds.AttentionListSize, cancellationToken);

        var names = await _analytics.GetUserNamesIncludingDeletedAsync(
            overdue.Select(task => task.AssignedToUserId).Distinct().ToList(),
            cancellationToken);
        var projectNames = await _analytics.GetProjectNamesIncludingDeletedAsync(
            overdue.Where(task => task.ProjectId != null).Select(task => task.ProjectId!.Value).Distinct().ToList(),
            cancellationToken);

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
