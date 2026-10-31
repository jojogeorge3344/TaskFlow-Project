using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Repositories.Dashboard.Analytics;

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

/// <summary>The tasks a dashboard reads, decided by its scope: with the date range, and the same set without it (for the
/// trend, which spans its own range).</summary>
public sealed record ScopedTasks(
    AnalyticsTaskSet Tasks,
    AnalyticsTaskSet TasksIgnoringDates,
    ResolvedAnalyticsFilter Filter);

public interface IDashboardScope
{
    Task<ScopedTasks> ScopeAsync(AnalyticsFilter filter, CancellationToken cancellationToken);
}
