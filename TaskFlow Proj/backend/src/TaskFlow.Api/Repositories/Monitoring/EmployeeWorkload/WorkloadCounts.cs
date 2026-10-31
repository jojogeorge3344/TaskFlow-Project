namespace TaskFlow.Api.Repositories.Monitoring.EmployeeWorkload;

/// <summary>One employee's live tasks by kind: open, open and high or critical priority, open past the deadline, completed.</summary>
public record WorkloadCounts(int UserId, int Active, int HighPriority, int Overdue, int Completed);
