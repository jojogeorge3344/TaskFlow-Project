using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Monitoring.EmployeeWorkload;

/// <summary>What the Employee Workload screen reads.</summary>
public interface IWorkloadRepository : IBaseRepository<User>
{
    /// <summary>Active users whose role does not hold <paramref name="viewAllPermissionKey"/>, optionally only those on a
    /// project, by name.</summary>
    Task<IReadOnlyList<WorkloadEmployee>> GetEmployeesAsync(
        string viewAllPermissionKey,
        int? projectId,
        CancellationToken cancellationToken = default);

    /// <summary>The task counts of each of these people that has any live task, with "overdue" measured at
    /// <paramref name="now"/>.</summary>
    Task<IReadOnlyList<WorkloadCounts>> GetTaskCountsAsync(
        IReadOnlyCollection<int> userIds,
        DateTime now,
        CancellationToken cancellationToken = default);
}
