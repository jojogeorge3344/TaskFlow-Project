using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Rules;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Analytics;

public class AdminDashboardScope : IDashboardScope
{
    private const int DefaultRangeDays = 30;
    private const int MaxRangeDays = 3660;

    private readonly AppDbContext _dbContext;
    private readonly IAppClock _clock;

    public AdminDashboardScope(AppDbContext dbContext, IAppClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<ScopedTasks> ScopeAsync(AnalyticsFilter filter, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(filter, cancellationToken);

        var withoutDates = _dbContext.Tasks
            .AsNoTracking()
            .Where(TaskBucketRules.IsCounted())
            .ApplyEntityFilters(_dbContext, resolved.DepartmentId, resolved.ProjectId, resolved.EmployeeId, resolved.ManagerId);
        var tasks = withoutDates.ApplyDateRange(resolved.Basis, resolved.FromUtc, resolved.ToUtcExclusive);

        return new ScopedTasks(tasks, withoutDates, resolved);
    }

    private async Task<ResolvedAnalyticsFilter> ResolveAsync(AnalyticsFilter filter, CancellationToken cancellationToken)
    {
        var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
        var nowUtc = _clock.UtcNow;
        var today = ReminderCalculator.LocalDate(nowUtc, timeZone);

        var to = filter.To ?? today;
        var from = filter.From ?? to.AddDays(-(DefaultRangeDays - 1));

        if (from > to)
        {
            throw new AnalyticsFilterException("The end date cannot be before the start date.");
        }

        if (to.DayNumber - from.DayNumber >= MaxRangeDays)
        {
            throw new AnalyticsFilterException("The date range is too long.");
        }

        if (!Enum.IsDefined(filter.Basis))
        {
            throw new AnalyticsFilterException("The date basis is not valid.");
        }

        await EnsureExistsAsync(
            filter.DepartmentId,
            id => _dbContext.Departments.AnyAsync(department => department.Id == id, cancellationToken),
            "department");
        await EnsureExistsAsync(
            filter.ProjectId,
            id => _dbContext.Projects.AnyAsync(project => project.Id == id, cancellationToken),
            "project");
        await EnsureExistsAsync(
            filter.EmployeeId,
            id => _dbContext.Users.AnyAsync(user => user.Id == id, cancellationToken),
            "employee");
        await EnsureExistsAsync(
            filter.ManagerId,
            id => _dbContext.Users.AnyAsync(user => user.Id == id, cancellationToken),
            "manager");

        return new ResolvedAnalyticsFilter(
            from,
            to,
            ReminderCalculator.LocalMidnightUtc(from, timeZone),
            ReminderCalculator.LocalMidnightUtc(to.AddDays(1), timeZone),
            filter.Basis,
            filter.DepartmentId,
            filter.ProjectId,
            filter.EmployeeId,
            filter.ManagerId,
            filter.AllEmployees,
            today,
            nowUtc,
            timeZone);
    }

    private static async System.Threading.Tasks.Task EnsureExistsAsync(
        int? id,
        Func<int, System.Threading.Tasks.Task<bool>> exists,
        string kind)
    {
        if (id is int value && !await exists(value))
        {
            throw new AnalyticsEntityNotFoundException($"The {kind} does not exist.");
        }
    }
}
