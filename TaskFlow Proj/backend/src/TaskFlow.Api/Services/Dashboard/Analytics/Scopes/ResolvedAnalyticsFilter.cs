using TaskFlow.Api.Dtos.Dashboard.Analytics;

namespace TaskFlow.Api.Services.Dashboard.Analytics.Scopes;

public sealed record ResolvedAnalyticsFilter(
    DateOnly From,
    DateOnly To,
    DateTime FromUtc,
    DateTime ToUtcExclusive,
    AnalyticsDateBasis Basis,
    int? DepartmentId,
    int? ProjectId,
    int? EmployeeId,
    int? ManagerId,
    bool AllEmployees,
    DateOnly Today,
    DateTime NowUtc,
    TimeZoneInfo TimeZone);
