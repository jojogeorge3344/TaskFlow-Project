namespace TaskFlow.Application.Dashboard.Analytics;

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
    int? ManagerId = null);

public class AnalyticsFilterException(string message) : Exception(message);

public class AnalyticsEntityNotFoundException(string message) : Exception(message);
