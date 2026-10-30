using TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Dashboard;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Persistence;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private const int UpcomingDeadlineWindowDays = 7;

    private readonly AppDbContext _dbContext;
    private readonly IAppClock _clock;
    private readonly IManagerDashboardScope _managerScope;

    public DashboardService(AppDbContext dbContext, IAppClock clock, IManagerDashboardScope managerScope)
    {
        _dbContext = dbContext;
        _clock = clock;
        _managerScope = managerScope;
    }

    public async Task<AdminDashboardDto> GetAdminStatsAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var totalEmployees = await _dbContext.Users.CountAsync(cancellationToken);
        var activeEmployees = await _dbContext.Users.CountAsync(user => user.IsActive, cancellationToken);
        var totalProjects = await _dbContext.Projects.CountAsync(cancellationToken);

        var tasks = ActiveTasks();

        var totalTasks = await tasks.CountAsync(cancellationToken);
        var pendingTasks = await tasks.CountAsync(
            task => task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled,
            cancellationToken);
        var completedTasks = await tasks.CountAsync(task => task.Status == TaskStatus.Completed, cancellationToken);
        var overdueTasks = await tasks.CountAsync(IsOverdue(now), cancellationToken);

        return new AdminDashboardDto(
            totalEmployees,
            activeEmployees,
            totalProjects,
            totalTasks,
            pendingTasks,
            completedTasks,
            overdueTasks);
    }

    public async Task<ManagerDashboardDto> GetManagerStatsAsync(int managerId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var (today, tomorrow) = await TodayBoundsAsync(cancellationToken);
        var upcomingHorizon = await DayStartUtcAsync(UpcomingDeadlineWindowDays + 1, cancellationToken);

        var tasks = _managerScope.TeamTasks(ActiveTasks(), managerId);
        var openTasks = tasks.Where(IsOpen());

        var totalTasks = await tasks.CountAsync(cancellationToken);
        var pendingTasks = await openTasks.CountAsync(cancellationToken);
        var tasksDueToday = await openTasks.CountAsync(
            task => task.Deadline >= today && task.Deadline < tomorrow,
            cancellationToken);
        var upcomingDeadlines = await openTasks.CountAsync(
            task => task.Deadline >= tomorrow && task.Deadline < upcomingHorizon,
            cancellationToken);
        var overdueTasks = await openTasks.CountAsync(IsOverdue(now), cancellationToken);
        var completedTasks = await tasks.CountAsync(task => task.Status == TaskStatus.Completed, cancellationToken);

        return new ManagerDashboardDto(
            totalTasks,
            pendingTasks,
            tasksDueToday,
            upcomingDeadlines,
            overdueTasks,
            completedTasks);
    }

    public async Task<EmployeeDashboardDto> GetEmployeeStatsAsync(int userId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var (today, tomorrow) = await TodayBoundsAsync(cancellationToken);

        var assigned = ActiveTasks().Where(task =>
            task.AssignedToUserId == userId ||
            _dbContext.TaskRecipients.Any(recipient => recipient.TaskId == task.Id && recipient.UserId == userId));

        var openAssigned = assigned.Where(IsOpen());

        var assignedTasks = await assigned.CountAsync(cancellationToken);
        var pendingTasks = await openAssigned.CountAsync(cancellationToken);
        var tasksDueToday = await openAssigned.CountAsync(
            task => task.Deadline >= today && task.Deadline < tomorrow,
            cancellationToken);
        var overdueTasks = await openAssigned.CountAsync(IsOverdue(now), cancellationToken);
        var inProgressTasks = await assigned.CountAsync(
            task => task.Status == TaskStatus.InProgress || task.Status == TaskStatus.InQA,
            cancellationToken);
        var completedTasks = await assigned.CountAsync(task => task.Status == TaskStatus.Completed, cancellationToken);

        return new EmployeeDashboardDto(
            assignedTasks,
            pendingTasks,
            tasksDueToday,
            overdueTasks,
            inProgressTasks,
            completedTasks);
    }

    /// <summary>Start of today and of tomorrow in the application timezone, as UTC instants (BR-17), so
    /// "due today" matches the deadline reminders.</summary>
    private async Task<(DateTime Today, DateTime Tomorrow)> TodayBoundsAsync(CancellationToken cancellationToken) =>
        (await DayStartUtcAsync(0, cancellationToken), await DayStartUtcAsync(1, cancellationToken));

    /// <summary>UTC instant at which the local day <paramref name="daysFromToday"/> days from today begins.</summary>
    private async Task<DateTime> DayStartUtcAsync(int daysFromToday, CancellationToken cancellationToken)
    {
        var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
        var today = ReminderCalculator.LocalDate(_clock.UtcNow, timeZone);
        return ReminderCalculator.LocalMidnightUtc(today.AddDays(daysFromToday), timeZone);
    }

    private IQueryable<TaskEntity> ActiveTasks() =>
        _dbContext.Tasks.Where(task => !task.IsDeleted);

    private static System.Linq.Expressions.Expression<Func<TaskEntity, bool>> IsOpen() =>
        task => task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled;

    private static System.Linq.Expressions.Expression<Func<TaskEntity, bool>> IsOverdue(DateTime now) =>
        task => task.Deadline < now && task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled;
}
