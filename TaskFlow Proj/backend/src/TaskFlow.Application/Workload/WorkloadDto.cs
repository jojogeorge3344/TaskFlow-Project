using TaskFlow.Application.Users;

namespace TaskFlow.Application.Workload;

/// <summary>One employee's task load for the Employee Workload screen (Screen 16). Counts use the
/// same open/overdue rules as the dashboards so the figures agree across screens. Projects lists every
/// project the employee is assigned to with its percentage; AllocatedPercent is their total.</summary>
public record EmployeeWorkloadDto(
    int UserId,
    string FullName,
    IReadOnlyList<ProjectAllocationDto> Projects,
    int AllocatedPercent,
    int ActiveTasks,
    int HighPriorityTasks,
    int OverdueTasks,
    int CompletedTasks);
