using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Dtos.Dashboard;
using TaskFlow.Api.Repositories.Dashboard;
using TaskFlow.Api.Services.Dashboard.Analytics;
using TaskFlow.Api.Services.Settings.ApplicationSettings;

namespace TaskFlow.Api.Services.Dashboard;

public class DashboardService : IDashboardService
{
    private const int UpcomingDeadlineWindowDays = 7;

    private readonly IDashboardRepository _dashboard;
    private readonly IAppClock _clock;
    private readonly IManagerDashboardScope _managerScope;

    public DashboardService(IDashboardRepository dashboard, IAppClock clock, IManagerDashboardScope managerScope)
    {
        _dashboard = dashboard;
        _clock = clock;
        _managerScope = managerScope;
    }

    public async Task<AdminDashboardDto> GetAdminStatsAsync(CancellationToken cancellationToken)
    {
        var totalEmployees = await _dashboard.CountUsersAsync(activeOnly: false, cancellationToken);
        var activeEmployees = await _dashboard.CountUsersAsync(activeOnly: true, cancellationToken);
        var totalProjects = await _dashboard.CountProjectsAsync(cancellationToken);

        var tasks = await _dashboard.CountTasksAsync(new DashboardTaskScope(), await BoundsAsync(cancellationToken), cancellationToken);

        return new AdminDashboardDto(
            totalEmployees,
            activeEmployees,
            totalProjects,
            tasks.Total,
            tasks.Open,
            tasks.Completed,
            tasks.Overdue);
    }

    public async Task<ManagerDashboardDto> GetManagerStatsAsync(int managerId, CancellationToken cancellationToken)
    {
        var team = await _managerScope.TeamUserIdsAsync(managerId, cancellationToken);
        var tasks = await _dashboard.CountTasksAsync(
            new DashboardTaskScope(TeamUserIds: team),
            await BoundsAsync(cancellationToken),
            cancellationToken);

        return new ManagerDashboardDto(
            tasks.Total,
            tasks.Open,
            tasks.DueToday,
            tasks.Upcoming,
            tasks.Overdue,
            tasks.Completed);
    }

    public async Task<EmployeeDashboardDto> GetEmployeeStatsAsync(int userId, CancellationToken cancellationToken)
    {
        var tasks = await _dashboard.CountTasksAsync(
            new DashboardTaskScope(OwnUserId: userId),
            await BoundsAsync(cancellationToken),
            cancellationToken);

        return new EmployeeDashboardDto(
            tasks.Total,
            tasks.Open,
            tasks.DueToday,
            tasks.Overdue,
            tasks.InProgress,
            tasks.Completed);
    }

    /// <summary>Now, and the start of today, of tomorrow and of the day after the upcoming window in the application
    /// timezone, as UTC instants (BR-17), so "due today" matches the deadline reminders.</summary>
    private async Task<DashboardDayBounds> BoundsAsync(CancellationToken cancellationToken)
    {
        var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
        var now = _clock.UtcNow;
        var today = ReminderCalculator.LocalDate(now, timeZone);
        return new DashboardDayBounds(
            now,
            ReminderCalculator.LocalMidnightUtc(today, timeZone),
            ReminderCalculator.LocalMidnightUtc(today.AddDays(1), timeZone),
            ReminderCalculator.LocalMidnightUtc(today.AddDays(UpcomingDeadlineWindowDays + 1), timeZone));
    }
}
