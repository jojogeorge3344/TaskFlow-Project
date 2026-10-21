namespace TaskFlow.Application.Workload;

/// <summary>One employee's task load for the Employee Workload screen (Screen 16). Counts use the
/// same open/overdue rules as the dashboards so the figures agree across screens.</summary>
public record EmployeeWorkloadDto(
    int UserId,
    string FullName,
    int? ProjectId,
    string? ProjectName,
    int ActiveTasks,
    int HighPriorityTasks,
    int OverdueTasks,
    int CompletedTasks);
