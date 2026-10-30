using TaskEntity = TaskFlow.Api.Models.Entities.Task;

namespace TaskFlow.Application.Dashboard.Analytics;

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

public sealed record ScopedTasks(
    IQueryable<TaskEntity> Tasks,
    IQueryable<TaskEntity> TasksIgnoringDates,
    ResolvedAnalyticsFilter Filter);

public interface IDashboardScope
{
    Task<ScopedTasks> ScopeAsync(AnalyticsFilter filter, CancellationToken cancellationToken);
}
