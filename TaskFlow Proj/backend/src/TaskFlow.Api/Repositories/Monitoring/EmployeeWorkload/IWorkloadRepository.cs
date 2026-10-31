using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Monitoring.EmployeeWorkload;

/// <summary>An employee on the workload screen with the projects they are allocated to.</summary>
public record WorkloadEmployee(int Id, string FullName, IReadOnlyList<ProjectAllocationDto> Projects);

/// <summary>One employee's live tasks by kind: open, open and high or critical priority, open past the deadline, completed.</summary>
public record WorkloadCounts(int UserId, int Active, int HighPriority, int Overdue, int Completed);

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
