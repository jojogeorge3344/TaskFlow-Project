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
    int? ManagerId = null,
    bool AllEmployees = false);

public class AnalyticsFilterException(string message) : TaskFlow.Api.Common.Exceptions.BusinessRuleException(message);

public class AnalyticsEntityNotFoundException(string message) : TaskFlow.Api.Common.Exceptions.NotFoundException(message);

public class AnalyticsForbiddenException(string message) : TaskFlow.Api.Common.Exceptions.ForbiddenException(message);
