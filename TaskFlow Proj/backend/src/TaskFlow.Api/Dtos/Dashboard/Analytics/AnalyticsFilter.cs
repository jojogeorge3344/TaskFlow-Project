namespace TaskFlow.Api.Dtos.Dashboard.Analytics;

public enum AnalyticsDateBasis
{
    Created,
    Deadline,
    Completed
}

public record AnalyticsFilter(
    DateOnly? From = null,
    DateOnly? To = null,
    AnalyticsDateBasis Basis = AnalyticsDateBasis.Created,
    int? DepartmentId = null,
    int? ProjectId = null,
    int? EmployeeId = null,
    int? ManagerId = null,
    bool AllEmployees = false);

