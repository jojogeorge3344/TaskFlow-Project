namespace TaskFlow.Api.Repositories.Dashboard;

/// <summary>The live tasks a dashboard card reads: every one, those assigned to or notifying a team member, or those
/// assigned to or notifying one person.</summary>
public record DashboardTaskScope(IReadOnlyList<int>? TeamUserIds = null, int? OwnUserId = null);

/// <summary>The local day bounds a dashboard counts against, as UTC instants.</summary>
public record DashboardDayBounds(DateTime Now, DateTime TodayStart, DateTime TomorrowStart, DateTime UpcomingHorizon);

/// <summary>Counts of the scoped live tasks. Open means neither completed nor cancelled; due today, upcoming (tomorrow up
/// to the horizon) and overdue count open tasks only.</summary>
public record DashboardTaskCounts(int Total, int Open, int DueToday, int Upcoming, int Overdue, int InProgress, int Completed);

/// <summary>What the dashboard cards read.</summary>
public interface IDashboardRepository : IBaseRepository<TaskFlow.Api.Models.Entities.Task>
{
    Task<int> CountUsersAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<int> CountProjectsAsync(CancellationToken cancellationToken = default);

    Task<DashboardTaskCounts> CountTasksAsync(
        DashboardTaskScope scope,
        DashboardDayBounds bounds,
        CancellationToken cancellationToken = default);
}
