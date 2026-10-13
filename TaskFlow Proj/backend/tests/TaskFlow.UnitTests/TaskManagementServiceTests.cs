using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Enums;
using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class TaskManagementServiceTests
{
    [Fact]
    public async Task CreateAsync_Manager_CreatesTaskWithCodeRecipientsActivityAndNotification()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tm-mgr", Role.Manager);
        var assignee = harness.AddUser("tm-assignee");
        var extra = harness.AddUser("tm-extra");

        var request = new CreateTaskRequest(
            "  Build report  ",
            "  Body  ",
            assignee.Id,
            TaskPriority.High,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(3),
            8m,
            new[] { assignee.Id, extra.Id },
            3,
            1,
            ReminderFrequency.Weekly);

        var details = await harness.TaskService.CreateAsync(request, manager.Id, nameof(Role.Manager), CancellationToken.None);

        Assert.NotEqual(0, details.Id);
        Assert.StartsWith("TSK-", details.TaskCode);
        Assert.Equal("Build report", details.Title);
        Assert.Equal("Body", details.Description);
        Assert.Equal(assignee.Id, details.AssignedToUserId);
        Assert.Equal(assignee.FullName, details.AssignedToName);
        Assert.Equal(manager.FullName, details.CreatedByName);
        Assert.Equal(TaskStatus.ToDo, details.Status);

        // The assignee is always a recipient and duplicates collapse to one entry.
        Assert.Equal(2, details.Recipients.Count);
        Assert.Contains(details.Recipients, recipient => recipient.UserId == assignee.Id);
        Assert.Contains(details.Recipients, recipient => recipient.UserId == extra.Id);

        var activity = await harness.ActivityService.GetForTaskAsync(details.Id, new ActivityFilter(), CancellationToken.None);
        Assert.Contains(activity, entry => entry.EventType == nameof(TaskActivityEventType.Created));
        Assert.Contains(activity, entry => entry.EventType == nameof(TaskActivityEventType.Assigned));

        var notification = await harness.Db.Notifications
            .SingleAsync(candidate => candidate.UserId == assignee.Id && candidate.TaskId == details.Id);
        Assert.Equal(NotificationType.TaskAssigned, notification.Type);

        var stored = await harness.Db.Tasks.SingleAsync(task => task.Id == details.Id);
        Assert.Equal(3, stored.FirstReminderDays);
        Assert.Equal(1, stored.SecondReminderDays);
        Assert.Equal(ReminderFrequency.Weekly, stored.OverdueReminderFrequency);
    }

    [Fact]
    public async Task CreateAsync_DefaultReminders_AreApplied()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tm-mgr2", Role.Manager);
        var assignee = harness.AddUser("tm-assignee2");

        var request = new CreateTaskRequest(
            "Default reminders",
            null,
            assignee.Id,
            TaskPriority.Medium,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2),
            4m);

        var details = await harness.TaskService.CreateAsync(request, manager.Id, nameof(Role.Manager), CancellationToken.None);

        var stored = await harness.Db.Tasks.SingleAsync(task => task.Id == details.Id);
        Assert.Equal(2, stored.FirstReminderDays);
        Assert.Equal(1, stored.SecondReminderDays);
        Assert.Equal(ReminderFrequency.Daily, stored.OverdueReminderFrequency);
    }

    [Fact]
    public async Task CreateAsync_Employee_ThrowsUnauthorized()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var employee = harness.AddUser("tm-emp");
        var assignee = harness.AddUser("tm-assignee3");

        var request = new CreateTaskRequest(
            "Nope",
            null,
            assignee.Id,
            TaskPriority.Low,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2),
            1m);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.TaskService.CreateAsync(request, employee.Id, nameof(Role.Employee), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_InactiveAssignee_ThrowsInvalidOperation()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tm-mgr3", Role.Manager);
        var inactive = harness.AddUser("tm-inactive", isActive: false);

        var request = new CreateTaskRequest(
            "Inactive target",
            null,
            inactive.Id,
            TaskPriority.Low,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2),
            1m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.TaskService.CreateAsync(request, manager.Id, nameof(Role.Manager), CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_Employee_ReturnsOnlyScopedTasks()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tl-mgr", Role.Manager);
        var employee = harness.AddUser("tl-emp");
        var other = harness.AddUser("tl-other");
        harness.AddTask(employee.Id, manager.Id, title: "Mine");
        harness.AddTask(other.Id, manager.Id, title: "Not mine");

        var result = await harness.TaskService.ListAsync(
            new TaskListFilter(), employee.Id, nameof(Role.Employee), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("Mine", item.Title);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task ListAsync_AppliesFiltersSearchAndPaging()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tl-mgr2", Role.Manager);
        var employee = harness.AddUser("tl-emp2");
        harness.AddTask(employee.Id, manager.Id, title: "Alpha report", status: TaskStatus.InProgress, priority: TaskPriority.High);
        harness.AddTask(employee.Id, manager.Id, title: "Beta task", status: TaskStatus.ToDo);

        var byStatus = await harness.TaskService.ListAsync(
            new TaskListFilter(Status: TaskStatus.InProgress), manager.Id, nameof(Role.Manager), CancellationToken.None);
        Assert.All(byStatus.Items, item => Assert.Equal(TaskStatus.InProgress, item.Status));

        var bySearch = await harness.TaskService.ListAsync(
            new TaskListFilter(Search: "Alpha"), manager.Id, nameof(Role.Manager), CancellationToken.None);
        Assert.Contains(bySearch.Items, item => item.Title == "Alpha report");

        var byPriority = await harness.TaskService.ListAsync(
            new TaskListFilter(Priority: TaskPriority.High), manager.Id, nameof(Role.Manager), CancellationToken.None);
        Assert.Contains(byPriority.Items, item => item.Title == "Alpha report");

        var paged = await harness.TaskService.ListAsync(
            new TaskListFilter(Page: 1, PageSize: 1), manager.Id, nameof(Role.Manager), CancellationToken.None);
        Assert.Single(paged.Items);
        Assert.Equal(2, paged.Total);
        Assert.Equal(1, paged.PageSize);
    }

    [Fact]
    public async Task ListAsync_MarksOverdueTasks()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tl-mgr3", Role.Manager);
        var employee = harness.AddUser("tl-emp3");
        harness.AddTask(employee.Id, manager.Id, title: "Late task", deadline: DateTime.UtcNow.AddDays(-2));
        harness.AddTask(employee.Id, manager.Id, title: "Future task", deadline: DateTime.UtcNow.AddDays(2));

        var result = await harness.TaskService.ListAsync(
            new TaskListFilter(Search: "task"), manager.Id, nameof(Role.Manager), CancellationToken.None);

        var late = Assert.Single(result.Items, item => item.Title == "Late task");
        Assert.True(late.IsOverdue);
        Assert.Equal("Overdue", late.Stage);

        var future = Assert.Single(result.Items, item => item.Title == "Future task");
        Assert.False(future.IsOverdue);
        Assert.Equal("Upcoming", future.Stage);
    }

    [Fact]
    public async Task CreateAsync_TwoTasks_GetDistinctCodes()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tm-mgr4", Role.Manager);
        var assignee = harness.AddUser("tm-assignee4");

        CreateTaskRequest Request(string title) => new(
            title, null, assignee.Id, TaskPriority.Low,
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 1m);

        var first = await harness.TaskService.CreateAsync(Request("First"), manager.Id, nameof(Role.Manager), CancellationToken.None);
        var second = await harness.TaskService.CreateAsync(Request("Second"), manager.Id, nameof(Role.Manager), CancellationToken.None);

        Assert.Equal($"TSK-{first.Id:D6}", first.TaskCode);
        Assert.Equal($"TSK-{second.Id:D6}", second.TaskCode);
        Assert.NotEqual(first.TaskCode, second.TaskCode);
    }

    [Fact]
    public async Task ListAsync_DeadlineRange_ReturnsOnlyTasksInRange()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tl-mgr4", Role.Manager);
        var employee = harness.AddUser("tl-emp4");
        harness.AddTask(employee.Id, manager.Id, title: "Soon", deadline: DateTime.UtcNow.AddDays(2));
        harness.AddTask(employee.Id, manager.Id, title: "Later", deadline: DateTime.UtcNow.AddDays(20));

        var result = await harness.TaskService.ListAsync(
            new TaskListFilter(DeadlineFrom: DateTime.UtcNow, DeadlineTo: DateTime.UtcNow.AddDays(7)),
            manager.Id, nameof(Role.Manager), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("Soon", item.Title);
    }

    [Fact]
    public async Task GetAssigneesAsync_Employee_ReturnsOnlyAssigneesInScope()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("ta-mgr", Role.Manager);
        var employee = harness.AddUser("ta-emp");
        var other = harness.AddUser("ta-other");
        harness.AddTask(employee.Id, manager.Id);
        harness.AddTask(other.Id, manager.Id);

        var forEmployee = await harness.TaskService.GetAssigneesAsync(employee.Id, nameof(Role.Employee), CancellationToken.None);
        var forManager = await harness.TaskService.GetAssigneesAsync(manager.Id, nameof(Role.Manager), CancellationToken.None);

        Assert.Equal(employee.Id, Assert.Single(forEmployee).UserId);
        Assert.Equal(2, forManager.Count);
    }
}
