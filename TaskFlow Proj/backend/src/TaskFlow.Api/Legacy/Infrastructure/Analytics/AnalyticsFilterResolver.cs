using TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Infrastructure.Analytics;

/// <summary>Turns the raw dashboard filter into resolved dates and validates the date range, shared by the Admin and
/// Manager scopes so both read the same default range, limits and timezone rules.</summary>
public class AnalyticsFilterResolver
{
    private const int DefaultRangeDays = 30;
    private const int MaxRangeDays = 3660;

    private readonly AppDbContext _dbContext;
    private readonly IAppClock _clock;

    public AnalyticsFilterResolver(AppDbContext dbContext, IAppClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    /// <summary>Resolves the filter. With <paramref name="checkEntitiesExist"/> every named department, project, employee
    /// and manager must exist (404 otherwise); the Manager scope validates its own ids against the team instead.</summary>
    public async Task<ResolvedAnalyticsFilter> ResolveAsync(
        AnalyticsFilter filter,
        bool checkEntitiesExist,
        CancellationToken cancellationToken)
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

        if (checkEntitiesExist)
        {
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
        }

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
