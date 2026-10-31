namespace TaskFlow.Api.Common.Exceptions;

public class AnalyticsFilterException(string message) : TaskFlow.Api.Common.Exceptions.BusinessRuleException(message);

public class AnalyticsEntityNotFoundException(string message) : TaskFlow.Api.Common.Exceptions.NotFoundException(message);

public class AnalyticsForbiddenException(string message) : TaskFlow.Api.Common.Exceptions.ForbiddenException(message);
