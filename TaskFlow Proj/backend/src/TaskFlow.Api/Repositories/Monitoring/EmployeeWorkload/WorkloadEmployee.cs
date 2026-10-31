using TaskFlow.Api.Dtos.Management.EmployeeManagement;

namespace TaskFlow.Api.Repositories.Monitoring.EmployeeWorkload;

/// <summary>An employee on the workload screen with the projects they are allocated to.</summary>
public record WorkloadEmployee(int Id, string FullName, IReadOnlyList<ProjectAllocationDto> Projects);
