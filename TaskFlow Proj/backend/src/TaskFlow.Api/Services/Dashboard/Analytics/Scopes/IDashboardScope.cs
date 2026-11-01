using TaskFlow.Api.Dtos.Dashboard.Analytics;

namespace TaskFlow.Api.Services.Dashboard.Analytics.Scopes;

public interface IDashboardScope
{
    Task<ScopedTasks> ScopeAsync(AnalyticsFilter filter, CancellationToken cancellationToken);
}
