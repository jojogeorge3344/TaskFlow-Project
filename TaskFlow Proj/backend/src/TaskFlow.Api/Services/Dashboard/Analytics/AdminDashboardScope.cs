using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Repositories.Interfaces.Dashboard.Analytics;
using TaskFlow.Api.Services.Interfaces.Dashboard.Analytics;
using TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings;

namespace TaskFlow.Api.Services.Dashboard.Analytics;

/// <summary>The Administrator's scope: every counted task, narrowed only by the dashboard filters.</summary>
public class AdminDashboardScope : IDashboardScope
{
    private readonly AnalyticsFilterResolver _resolver;

    public AdminDashboardScope(IAnalyticsRepository analytics, IAppClock clock)
    {
        _resolver = new AnalyticsFilterResolver(analytics, clock);
    }

    public async Task<ScopedTasks> ScopeAsync(AnalyticsFilter filter, CancellationToken cancellationToken)
    {
        var resolved = await _resolver.ResolveAsync(filter, checkEntitiesExist: true, cancellationToken);
        var withoutDates = new AnalyticsTaskSet(
            DepartmentId: resolved.DepartmentId,
            ProjectId: resolved.ProjectId,
            EmployeeId: resolved.EmployeeId,
            ManagerId: resolved.ManagerId);
        var tasks = withoutDates with { Dates = new AnalyticsDateRange(resolved.Basis, resolved.FromUtc, resolved.ToUtcExclusive) };
        return new ScopedTasks(tasks, withoutDates, resolved);
    }
}
