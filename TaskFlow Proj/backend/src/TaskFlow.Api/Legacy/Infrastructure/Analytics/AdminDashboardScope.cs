using TaskFlow.Api.Repositories.Dashboard.Analytics;
using TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Infrastructure.Analytics;

public class AdminDashboardScope : IDashboardScope
{
    private readonly AppDbContext _dbContext;
    private readonly AnalyticsFilterResolver _resolver;

    public AdminDashboardScope(AppDbContext dbContext, IAppClock clock)
    {
        _dbContext = dbContext;
        _resolver = new AnalyticsFilterResolver(dbContext, clock);
    }

    public async Task<ScopedTasks> ScopeAsync(AnalyticsFilter filter, CancellationToken cancellationToken)
    {
        var resolved = await _resolver.ResolveAsync(filter, checkEntitiesExist: true, cancellationToken);

        var withoutDates = _dbContext.Tasks
            .AsNoTracking()
            .Where(TaskBucketRules.IsCounted())
            .ApplyEntityFilters(_dbContext, resolved.DepartmentId, resolved.ProjectId, resolved.EmployeeId, resolved.ManagerId);
        var tasks = withoutDates.ApplyDateRange(resolved.Basis, resolved.FromUtc, resolved.ToUtcExclusive);

        return new ScopedTasks(tasks, withoutDates, resolved);
    }
}
