using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Security;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Enums;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class TaskServiceTests
{
    [Fact]
    public async Task GetDetailsAsync_Manager_ReturnsFullDetails()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkmgr", SystemRoles.Manager);
        var assignee = harness.AddUser("tkasgn");
        var task = harness.AddTask(assignee.Id, manager.Id);
        harness.AddRecipient(task.Id, assignee.Id);

        var details = await harness.TaskService.GetDetailsAsync(task.Id, manager.Id, SystemRoles.Manager, CancellationToken.None);

        Assert.NotNull(details);
        Assert.Equal(task.Id, details.Id);
        Assert.Equal("Seed task", details.Title);
        Assert.Single(details.Recipients);
        Assert.Equal("Upcoming", details.Stage);
    }

    [Fact]
    public async Task GetDetailsAsync_UnrelatedEmployee_ReturnsNull()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkmgr", SystemRoles.Manager);
        var assignee = harness.AddUser("tkasgn2");
        var stranger = harness.AddUser("tkstranger");
        var task = harness.AddTask(assignee.Id, manager.Id);

        var details = await harness.TaskService.GetDetailsAsync(task.Id, stranger.Id, SystemRoles.Employee, CancellationToken.None);

        Assert.Null(details);
    }

    [Fact]
    public async Task GetDetailsAsync_AssigneeCreatorsAndRecipients_CanAccess()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkmgr2", SystemRoles.Manager);
        var assignee = harness.AddUser("tkasgn3");
        TestMemberships.ReportTo(harness.Db, assignee, manager.Id); // a manager reaches their own team's tasks
        var creator = harness.AddUser("tkcreator");
        var recipient = harness.AddUser("tkrecipient");
        var task = harness.AddTask(assignee.Id, creator.Id);
        harness.AddRecipient(task.Id, recipient.Id);

        Assert.NotNull(await harness.TaskService.GetDetailsAsync(task.Id, assignee.Id, SystemRoles.Employee, CancellationToken.None));
        Assert.NotNull(await harness.TaskService.GetDetailsAsync(task.Id, creator.Id, SystemRoles.Employee, CancellationToken.None));
        Assert.NotNull(await harness.TaskService.GetDetailsAsync(task.Id, recipient.Id, SystemRoles.Employee, CancellationToken.None));
        Assert.NotNull(await harness.TaskService.GetDetailsAsync(task.Id, manager.Id, SystemRoles.Manager, CancellationToken.None));
    }

    [Fact]
    public async Task Manager_CannotReadAnotherTeamsTaskButReadsTheirOwnTeamAndCreatedTasks()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkmgr-scope", SystemRoles.Manager);
        var member = harness.AddUser("tkmember-scope");
        TestMemberships.ReportTo(harness.Db, member, manager.Id);
        var foreign = harness.AddUser("tkforeign-scope");
        var team = harness.AddTask(member.Id, manager.Id, "Team task");
        var created = harness.AddTask(foreign.Id, manager.Id, "Created by me, assigned outside the team");
        var other = harness.AddTask(foreign.Id, foreign.Id, "Another team");

        Assert.NotNull(await harness.TaskService.GetDetailsAsync(team.Id, manager.Id, SystemRoles.Manager, CancellationToken.None));
        Assert.NotNull(await harness.TaskService.GetDetailsAsync(created.Id, manager.Id, SystemRoles.Manager, CancellationToken.None));
        Assert.Null(await harness.TaskService.GetDetailsAsync(other.Id, manager.Id, SystemRoles.Manager, CancellationToken.None));

        var board = await harness.TaskService.GetBoardAsync(manager.Id, SystemRoles.Manager, CancellationToken.None);
        Assert.DoesNotContain(board, card => card.Title == "Another team");
        Assert.Equal(2, board.Count);

        var list = await harness.TaskService.ListAsync(new TaskFlow.Application.Tasks.TaskListFilter(), manager.Id, SystemRoles.Manager, CancellationToken.None);
        Assert.Equal(2, list.Total);

        var assignees = await harness.TaskService.GetAssigneesAsync(manager.Id, SystemRoles.Manager, CancellationToken.None);
        Assert.Equal(2, assignees.Count);
    }

    [Fact]
    public async Task Administrator_StillSeesEveryTask()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var admin = harness.AddUser("tkadmin-scope", SystemRoles.Administrator);
        var one = harness.AddUser("tkone-scope");
        var task = harness.AddTask(one.Id, one.Id, "Anyone");

        Assert.NotNull(await harness.TaskService.GetDetailsAsync(task.Id, admin.Id, SystemRoles.Administrator, CancellationToken.None));
        Assert.Single(await harness.TaskService.GetBoardAsync(admin.Id, SystemRoles.Administrator, CancellationToken.None));
    }

    [Fact]
    public async Task GetBoardAsync_ManagerSeesAllTasks_EmployeeSeesOnlyScopedOnes()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkbm", SystemRoles.Manager);
        var assignee = harness.AddUser("tkba");
        var other = harness.AddUser("tkbo");

        harness.AddTask(assignee.Id, manager.Id, "Assigned to me");
        harness.AddTask(other.Id, manager.Id, "Assigned to someone else");

        var managerBoard = await harness.TaskService.GetBoardAsync(manager.Id, SystemRoles.Manager, CancellationToken.None);
        Assert.Equal(2, managerBoard.Count);

        var assigneeBoard = await harness.TaskService.GetBoardAsync(assignee.Id, SystemRoles.Employee, CancellationToken.None);
        var card = Assert.Single(assigneeBoard);
        Assert.Equal("Assigned to me", card.Title);
    }

    [Fact]
    public async Task GetBoardAsync_CardsCarryTheirProjectIdAndName()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var admin = harness.AddUser("tkbp-admin", SystemRoles.Administrator);
        var project = new TaskFlow.Domain.Entities.Project { Name = "Atlas", CreatedAt = DateTime.UtcNow };
        harness.Db.Projects.Add(project);
        harness.Db.SaveChanges();

        var withProject = harness.AddTask(admin.Id, admin.Id, "In a project");
        withProject.ProjectId = project.Id;
        harness.Db.SaveChanges();
        harness.AddTask(admin.Id, admin.Id, "No project");

        var board = await harness.TaskService.GetBoardAsync(admin.Id, SystemRoles.Administrator, CancellationToken.None);

        var card = Assert.Single(board, c => c.Title == "In a project");
        Assert.Equal(project.Id, card.ProjectId);
        Assert.Equal("Atlas", card.ProjectName);
        var bare = Assert.Single(board, c => c.Title == "No project");
        Assert.Null(bare.ProjectId);
        Assert.Null(bare.ProjectName);
    }

    [Fact]
    public async Task ChangeStatusAsync_AllowedTransition_UpdatesAndLogsActivity()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var assignee = harness.AddUser("tkcs1");
        var task = harness.AddTask(assignee.Id, assignee.Id, status: TaskStatus.ToDo);

        var card = await harness.TaskService.ChangeStatusAsync(
            task.Id,
            new UpdateTaskStatusRequest(TaskStatus.InProgress),
            assignee.Id,
            SystemRoles.Employee,
            CancellationToken.None);

        Assert.NotNull(card);
        Assert.Equal(TaskStatus.InProgress, card.Status);

        var reloaded = await harness.Db.Tasks.SingleAsync(candidate => candidate.Id == task.Id);
        Assert.Equal(TaskStatus.InProgress, reloaded.Status);

        var activity = await harness.ActivityService.GetForTaskAsync(
            task.Id,
            new ActivityFilter(EventType: TaskActivityEventType.StatusChanged),
            CancellationToken.None);
        Assert.Single(activity);
    }

    [Fact]
    public async Task ChangeStatusAsync_Completing_SetsCompletedAtAndLogsCompletion()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var assignee = harness.AddUser("tkcs2");
        var task = harness.AddTask(assignee.Id, assignee.Id, status: TaskStatus.InProgress);

        await harness.TaskService.ChangeStatusAsync(
            task.Id,
            new UpdateTaskStatusRequest(TaskStatus.Completed),
            assignee.Id,
            SystemRoles.Employee,
            CancellationToken.None);

        var reloaded = await harness.Db.Tasks.SingleAsync(candidate => candidate.Id == task.Id);
        Assert.NotNull(reloaded.CompletedAt);

        var completion = await harness.ActivityService.GetForTaskAsync(
            task.Id,
            new ActivityFilter(EventType: TaskActivityEventType.Completed),
            CancellationToken.None);
        Assert.Single(completion);
    }

    [Fact]
    public async Task ChangeStatusAsync_DisallowedTransition_Throws()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkcs3", SystemRoles.Manager);
        var task = harness.AddTask(manager.Id, manager.Id, status: TaskStatus.ToDo);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.TaskService.ChangeStatusAsync(
                task.Id,
                new UpdateTaskStatusRequest(TaskStatus.Completed),
                manager.Id,
                SystemRoles.Manager,
                CancellationToken.None));
    }

    [Fact]
    public async Task ChangeStatusAsync_UnrelatedEmployee_Throws()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkcs4", SystemRoles.Manager);
        var stranger = harness.AddUser("tkcs5");
        var task = harness.AddTask(manager.Id, manager.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.TaskService.ChangeStatusAsync(
                task.Id,
                new UpdateTaskStatusRequest(TaskStatus.InProgress),
                stranger.Id,
                SystemRoles.Employee,
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_ManagerEdit_AppliesChangesAndLogsRelevantActivity()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkup1", SystemRoles.Manager);
        var assignee = harness.AddUser("tkup2");
        var task = harness.AddTask(assignee.Id, manager.Id);

        var newDeadline = task.Deadline.AddDays(3);
        var details = await harness.TaskService.UpdateAsync(
            task.Id,
            new UpdateTaskRequest(
                "Updated title",
                "Updated description",
                TaskFlow.Domain.Enums.TaskPriority.High,
                task.StartDate,
                newDeadline,
                12m),
            manager.Id,
            SystemRoles.Manager,
            CancellationToken.None);

        Assert.NotNull(details);
        Assert.Equal("Updated title", details.Title);
        Assert.Equal(newDeadline, details.Deadline);
        Assert.Equal(TaskFlow.Domain.Enums.TaskPriority.High, details.Priority);

        var activity = await harness.ActivityService.GetForTaskAsync(task.Id, new ActivityFilter(), CancellationToken.None);
        Assert.Contains(activity, entry => entry.EventType == nameof(TaskActivityEventType.DeadlineChanged));
        Assert.Contains(activity, entry => entry.EventType == nameof(TaskActivityEventType.PriorityChanged));
    }

    [Fact]
    public async Task UpdateAsync_Employee_Throws()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkup3", SystemRoles.Manager);
        var assignee = harness.AddUser("tkup4");
        var task = harness.AddTask(assignee.Id, manager.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.TaskService.UpdateAsync(
                task.Id,
                new UpdateTaskRequest("x", null, TaskFlow.Domain.Enums.TaskPriority.Low, task.StartDate, task.Deadline, 8m),
                assignee.Id,
                SystemRoles.Employee,
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_InvalidDeadline_Throws()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkup5", SystemRoles.Manager);
        var task = harness.AddTask(manager.Id, manager.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.TaskService.UpdateAsync(
                task.Id,
                new UpdateTaskRequest(
                    "x",
                    null,
                    TaskFlow.Domain.Enums.TaskPriority.Low,
                    new DateTime(2026, 10, 15),
                    new DateTime(2026, 10, 10),
                    8m),
                manager.Id,
                SystemRoles.Manager,
                CancellationToken.None));
    }

    [Fact]
    public async Task ReassignAsync_Manager_UpdatesAssigneeRecipientsAndNotifies()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkras1", SystemRoles.Manager);
        var oldAssignee = harness.AddUser("tkras2");
        var newAssignee = harness.AddUser("tkras3");
        var task = harness.AddTask(oldAssignee.Id, manager.Id);

        var details = await harness.TaskService.ReassignAsync(
            task.Id,
            new ReassignTaskRequest(newAssignee.Id, "Handing over to the new analyst"),
            manager.Id,
            SystemRoles.Manager,
            CancellationToken.None);

        Assert.NotNull(details);
        Assert.Equal(newAssignee.Id, details.AssignedToUserId);
        Assert.Contains(details.Recipients, recipient => recipient.UserId == newAssignee.Id);

        var notifications = await harness.Db.Notifications
            .Where(notification => notification.UserId == newAssignee.Id)
            .ToListAsync();
        var reassigned = Assert.Single(notifications);
        Assert.Equal(NotificationType.Reassigned, reassigned.Type);

        var activity = await harness.ActivityService.GetForTaskAsync(
            task.Id,
            new ActivityFilter(EventType: TaskActivityEventType.Reassigned),
            CancellationToken.None);
        Assert.Single(activity);
    }

    [Fact]
    public async Task ReassignAsync_Employee_Throws()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkras4", SystemRoles.Manager);
        var assignee = harness.AddUser("tkras5");
        var other = harness.AddUser("tkras6");
        var task = harness.AddTask(assignee.Id, manager.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.TaskService.ReassignAsync(
                task.Id,
                new ReassignTaskRequest(other.Id, "Nope"),
                assignee.Id,
                SystemRoles.Employee,
                CancellationToken.None));
    }

    [Fact]
    public async Task ReassignAsync_InactiveTarget_Throws()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkras7", SystemRoles.Manager);
        var inactive = harness.AddUser("tkras8", isActive: false);
        var task = harness.AddTask(manager.Id, manager.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.TaskService.ReassignAsync(
                task.Id,
                new ReassignTaskRequest(inactive.Id, "Whoops"),
                manager.Id,
                SystemRoles.Manager,
                CancellationToken.None));
    }

    [Fact]
    public async Task SetRecipientsAsync_AddsAndRemoves_KeepingAssigneeIncluded()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkrc1", SystemRoles.Manager);
        var assignee = harness.AddUser("tkrc2");
        var extraA = harness.AddUser("tkrc3");
        var extraB = harness.AddUser("tkrc4");
        var task = harness.AddTask(assignee.Id, manager.Id);
        harness.AddRecipient(task.Id, extraA.Id);

        var updated = await harness.TaskService.SetRecipientsAsync(
            task.Id,
            new SetRecipientsRequest(new[] { extraB.Id }),
            manager.Id,
            SystemRoles.Manager,
            CancellationToken.None);

        Assert.True(updated);

        var recipients = await harness.TaskService.GetRecipientsAsync(task.Id, manager.Id, SystemRoles.Manager, CancellationToken.None);
        Assert.NotNull(recipients);
        Assert.Equal(2, recipients.Count);
        Assert.Contains(recipients, recipient => recipient.UserId == assignee.Id);
        Assert.Contains(recipients, recipient => recipient.UserId == extraB.Id);
        Assert.DoesNotContain(recipients, recipient => recipient.UserId == extraA.Id);
    }

    [Fact]
    public async Task GetRecipientsAsync_MissingTask_ReturnsNull()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkrc5", SystemRoles.Manager);

        var recipients = await harness.TaskService.GetRecipientsAsync(9999, manager.Id, SystemRoles.Manager, CancellationToken.None);

        Assert.Null(recipients);
    }

    [Fact]
    public async Task GetDetailsAsync_MissingTask_ReturnsNull()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("tkdt1", SystemRoles.Manager);

        var details = await harness.TaskService.GetDetailsAsync(9999, manager.Id, SystemRoles.Manager, CancellationToken.None);

        Assert.Null(details);
    }
}