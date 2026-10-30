namespace TaskFlow.Application.Dashboard.Analytics;

/// <summary>The Manager's authorised scope: a team (direct reports plus members of managed departments) and the tasks
/// assigned to or shared with it. Unlike <see cref="IDashboardScope"/> it needs the caller's id, so the id is explicit.</summary>
public interface IManagerDashboardScope
{
    /// <summary>The scoped task query for the manager. Throws <see cref="AnalyticsForbiddenException"/> when the
    /// employee or project filter is outside the scope and <see cref="AnalyticsEntityNotFoundException"/> when it does
    /// not exist. Any department or manager filter is ignored.</summary>
    Task<ScopedTasks> ScopeAsync(int managerId, AnalyticsFilter filter, CancellationToken cancellationToken);

    /// <summary>Restricts a task query to tasks assigned to or shared with the manager's team.</summary>
    IQueryable<TaskFlow.Api.Models.Entities.Task> TeamTasks(IQueryable<TaskFlow.Api.Models.Entities.Task> tasks, int managerId);

    /// <summary>Ids of the active users on the manager's team, distinct, as a composable query.</summary>
    IQueryable<int> TeamUserIds(int managerId);

    /// <summary>Ids of the projects the manager is assigned to plus those the team's tasks belong to.</summary>
    IQueryable<int> ProjectIds(int managerId);
}
