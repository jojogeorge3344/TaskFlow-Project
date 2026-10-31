using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Dashboard.Overview;

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
