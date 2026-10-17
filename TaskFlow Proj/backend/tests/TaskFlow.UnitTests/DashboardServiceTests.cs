using TaskFlow.Domain.Enums;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class DashboardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Today = new(2026, 6, 1);

    [Fact]
    public async Task GetAdminStatsAsync_ReturnsEmployeeDepartmentAndTaskCounts()
    {
        await using var harness = DashboardServiceHarness.Create(Now);
        harness.AddDepartment("Engineering");
        harness.AddDepartment("Operations");

        var admin = harness.AddUser("admin", role: Role.Administrator);
        harness.AddUser("active-employee");
        harness.AddUser("inactive-employee", isActive: false);

        harness.AddTask(admin.Id, new DateTime(2026, 6, 5), TaskStatus.ToDo);
        harness.AddTask(admin.Id, new DateTime(2026, 5, 1), TaskStatus.Completed);
        harness.AddTask(admin.Id, new DateTime(2026, 5, 20), TaskStatus.ToDo);
        harness.AddTask(admin.Id, new DateTime(2026, 5, 1), TaskStatus.Cancelled);
        harness.AddTask(admin.Id, Today.AddHours(18), TaskStatus.InProgress);
        harness.AddTask(admin.Id, new DateTime(2026, 5, 20), TaskStatus.ToDo, isDeleted: true);

        var stats = await harness.Service.GetAdminStatsAsync(CancellationToken.None);

        Assert.Equal(3, stats.TotalEmployees);
        Assert.Equal(2, stats.ActiveEmployees);
        Assert.Equal(2, stats.TotalDepartments);
        Assert.Equal(5, stats.TotalTasks);
        Assert.Equal(3, stats.PendingTasks);
        Assert.Equal(1, stats.CompletedTasks);
        Assert.Equal(1, stats.OverdueTasks);
    }

    [Fact]
    public async Task GetManagerStatsAsync_ReturnsTaskProgressCounts()
    {
        await using var harness = DashboardServiceHarness.Create(Now);
        var manager = harness.AddUser("manager", role: Role.Manager);

        harness.AddTask(manager.Id, Today.AddHours(18), TaskStatus.ToDo);          // due today, pending
        harness.AddTask(manager.Id, new DateTime(2026, 6, 3), TaskStatus.ToDo);   // upcoming, pending
        harness.AddTask(manager.Id, new DateTime(2026, 5, 30), TaskStatus.ToDo);  // overdue, pending
        harness.AddTask(manager.Id, new DateTime(2026, 5, 29), TaskStatus.Completed);
        harness.AddTask(manager.Id, new DateTime(2026, 6, 2), TaskStatus.Cancelled);

        var stats = await harness.Service.GetManagerStatsAsync(CancellationToken.None);

        Assert.Equal(5, stats.TotalTasks);
        Assert.Equal(3, stats.PendingTasks);
        Assert.Equal(1, stats.TasksDueToday);
        Assert.Equal(1, stats.UpcomingDeadlines);
        Assert.Equal(1, stats.OverdueTasks);
        Assert.Equal(1, stats.CompletedTasks);
    }

    [Fact]
    public async Task GetManagerStatsAsync_ExcludesDeletedTasks()
    {
        await using var harness = DashboardServiceHarness.Create(Now);
        var manager = harness.AddUser("manager", role: Role.Manager);

        harness.AddTask(manager.Id, new DateTime(2026, 6, 5), TaskStatus.ToDo);
        harness.AddTask(manager.Id, new DateTime(2026, 6, 5), TaskStatus.ToDo, isDeleted: true);

        var stats = await harness.Service.GetManagerStatsAsync(CancellationToken.None);

        Assert.Equal(1, stats.TotalTasks);
    }

    [Fact]
    public async Task GetEmployeeStatsAsync_ScopesToAssignedAndRecipientTasks()
    {
        await using var harness = DashboardServiceHarness.Create(Now);
        var employee = harness.AddUser("employee");
        var colleague = harness.AddUser("colleague");

        harness.AddTask(employee.Id, Today.AddHours(18), TaskStatus.InProgress);      // assigned, due today, in progress
        harness.AddTask(employee.Id, new DateTime(2026, 5, 25), TaskStatus.Completed);
        harness.AddTask(employee.Id, new DateTime(2026, 5, 28), TaskStatus.ToDo);     // overdue
        var shared = harness.AddTask(colleague.Id, new DateTime(2026, 6, 4), TaskStatus.ToDo);
        harness.AddRecipient(shared, employee);
        harness.AddTask(colleague.Id, new DateTime(2026, 6, 10), TaskStatus.ToDo);    // not visible
        harness.AddTask(employee.Id, new DateTime(2026, 5, 28), TaskStatus.ToDo, isDeleted: true);

        var stats = await harness.Service.GetEmployeeStatsAsync(employee.Id, CancellationToken.None);

        Assert.Equal(4, stats.AssignedTasks);
        Assert.Equal(3, stats.PendingTasks);
        Assert.Equal(1, stats.TasksDueToday);
        Assert.Equal(1, stats.OverdueTasks);
        Assert.Equal(1, stats.InProgressTasks);
        Assert.Equal(1, stats.CompletedTasks);
    }

    [Fact]
    public async Task GetEmployeeStatsAsync_WithNoTasks_ReturnsZeros()
    {
        await using var harness = DashboardServiceHarness.Create(Now);
        var employee = harness.AddUser("employee");

        var stats = await harness.Service.GetEmployeeStatsAsync(employee.Id, CancellationToken.None);

        Assert.Equal(0, stats.AssignedTasks);
        Assert.Equal(0, stats.PendingTasks);
        Assert.Equal(0, stats.TasksDueToday);
        Assert.Equal(0, stats.OverdueTasks);
        Assert.Equal(0, stats.InProgressTasks);
        Assert.Equal(0, stats.CompletedTasks);
    }

    [Fact]
    public async Task DueToday_UsesTheApplicationTimezoneDay()
    {
        // 19:00 UTC on 1 June is 00:30 IST on 2 June.
        await using var harness = DashboardServiceHarness.Create(new DateTimeOffset(2026, 6, 1, 19, 0, 0, TimeSpan.Zero), "Asia/Kolkata");
        var employee = harness.AddUser("ist-employee");

        // 05:00 UTC on 2 June is 10:30 IST on 2 June: due today in IST, tomorrow in UTC.
        harness.AddTask(employee.Id, new DateTime(2026, 6, 2, 5, 0, 0), TaskStatus.ToDo);

        // 20:00 UTC on 2 June is 01:30 IST on 3 June: upcoming in IST.
        harness.AddTask(employee.Id, new DateTime(2026, 6, 2, 20, 0, 0), TaskStatus.ToDo);

        var manager = await harness.Service.GetManagerStatsAsync(CancellationToken.None);
        var mine = await harness.Service.GetEmployeeStatsAsync(employee.Id, CancellationToken.None);

        Assert.Equal(1, manager.TasksDueToday);
        Assert.Equal(1, manager.UpcomingDeadlines);
        Assert.Equal(1, mine.TasksDueToday);
    }
}
