namespace TaskFlow.Application.Workload;

public interface IWorkloadService
{
    /// <summary>Per-employee task load for every active employee (Screen 16), optionally narrowed to
    /// one project. Employees carrying no tasks appear with zero counts.</summary>
    Task<IReadOnlyCollection<EmployeeWorkloadDto>> GetWorkloadAsync(
        int? projectId,
        CancellationToken cancellationToken);
}
