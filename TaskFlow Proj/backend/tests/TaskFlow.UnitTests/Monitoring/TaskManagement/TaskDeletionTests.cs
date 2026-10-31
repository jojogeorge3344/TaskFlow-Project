using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Services.Assistant;
using TaskFlow.Api.Dtos.Monitoring.TaskManagement.Discussion;
using TaskFlow.Api.Dtos.Monitoring.TaskManagement;
using TaskFlow.Api.Services.Communication.Notification.Reminders;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Services.Settings.ReminderSettings;
using TaskFlow.Api.Services.Settings.ApplicationSettings;
using TaskFlow.Api.Config;
using TaskFlow.Api.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Security;
using Task = System.Threading.Tasks.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;
using TaskFlow.UnitTests.Assistant;

namespace TaskFlow.UnitTests.Monitoring.TaskManagement;

public class TaskDeletionTests
{
    [Fact]
    public async Task Delete_SoftDeletesTheTaskAndRecordsWhoDidIt()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var admin = harness.AddUser("del-admin", SystemRoles.Administrator);
        var employee = harness.AddUser("del-emp");
        var task = harness.AddTask(employee.Id, admin.Id, "Remove me");

        var deleted = await harness.TaskService.DeleteAsync(task.Id, admin.Id, default);

        Assert.True(deleted);
        var row = await harness.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);
        Assert.True(row.IsDeleted);
        var entry = await harness.Db.TaskActivityLogs.AsNoTracking().SingleAsync(log => log.TaskId == task.Id && log.EventType == TaskActivityEventType.Deleted);
        Assert.Equal(admin.Id, entry.ActorUserId);
        Assert.Equal("Remove me", entry.OldValue);
    }

    [Fact]
    public async Task Delete_ReturnsFalseForAnUnknownOrAlreadyDeletedTask()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var admin = harness.AddUser("del-admin-2", SystemRoles.Administrator);
        var task = harness.AddTask(admin.Id, admin.Id);

        Assert.False(await harness.TaskService.DeleteAsync(9999, admin.Id, default));
        Assert.True(await harness.TaskService.DeleteAsync(task.Id, admin.Id, default));
        Assert.False(await harness.TaskService.DeleteAsync(task.Id, admin.Id, default));
        Assert.Equal(1, await harness.Db.TaskActivityLogs.CountAsync(log => log.TaskId == task.Id && log.EventType == TaskActivityEventType.Deleted));
    }

    [Fact]
    public async Task Delete_KeepsCommentsAndEarlierHistory()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var admin = harness.AddUser("del-admin-3", SystemRoles.Administrator);
        var task = harness.AddTask(admin.Id, admin.Id);
        await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("A comment"), admin.Id, default);
        var before = await harness.Db.TaskActivityLogs.CountAsync(log => log.TaskId == task.Id);

        await harness.TaskService.DeleteAsync(task.Id, admin.Id, default);

        Assert.Equal(1, await harness.Db.TaskComments.CountAsync(comment => comment.TaskId == task.Id));
        Assert.Equal(before + 1, await harness.Db.TaskActivityLogs.CountAsync(log => log.TaskId == task.Id));
    }

    [Fact]
    public async Task ADeletedTaskIsGoneFromDetailsListBoardAndAssignees()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var admin = harness.AddUser("del-admin-4", SystemRoles.Administrator);
        var keep = harness.AddTask(admin.Id, admin.Id, "Keep me");
        var remove = harness.AddTask(admin.Id, admin.Id, "Delete me");

        await harness.TaskService.DeleteAsync(remove.Id, admin.Id, default);

        Assert.Null(await harness.TaskService.GetDetailsAsync(remove.Id, admin.Id, SystemRoles.Administrator, default));
        var list = await harness.TaskService.ListAsync(new TaskListFilter(), admin.Id, SystemRoles.Administrator, default);
        Assert.Equal([keep.Id], list.Items.Select(item => item.Id));
        var board = await harness.TaskService.GetBoardAsync(admin.Id, SystemRoles.Administrator, default);
        Assert.Equal([keep.Id], board.Select(card => card.Id));
    }

    [Fact]
    public async Task ADeletedTaskNoLongerGeneratesDeadlineReminders()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var creator = harness.AddUser("del-rem-creator", SystemRoles.Administrator);
        var assignee = harness.AddUser("del-rem-assignee");
        var deadline = new DateTime(2026, 10, 15, 17, 0, 0, DateTimeKind.Utc);
        var task = harness.AddTask(assignee.Id, creator.Id, "Prepare report", TaskStatus.InProgress, deadline);
        harness.AddRecipient(task.Id, assignee.Id);
        var clock = new FixedTimeProvider(new DateTime(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc));
        var options = Options.Create(new ApplicationOptions { Timezone = "UTC" });
        var reminders = TestServices.Reminders(
            harness.Db,
            new AppClock(new ApplicationSettingsRepository(harness.Db), clock, options, NullLogger<AppClock>.Instance),
            new ReminderSettingsService(new ApplicationSettingsRepository(harness.Db), new UnitOfWork(harness.Db), clock, options),
            Options.Create(new TaskFlow.Api.Config.MailOptions { AppBaseUrl = "http://localhost:4200" }),
            NullLogger<ReminderService>.Instance);

        await harness.TaskService.DeleteAsync(task.Id, creator.Id, default);
        var written = await reminders.GenerateDueAsync();

        Assert.Equal(0, written);
        Assert.False(await harness.Db.Notifications.AnyAsync(notification => notification.TaskId == task.Id));
    }

    [Fact]
    public async Task ADeletedTaskIsNotOfferedToTheAssistant()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var admin = harness.AddUser("del-ai-admin", SystemRoles.Administrator);
        var keep = harness.AddTask(admin.Id, admin.Id, "Visible to TFA", TaskStatus.InProgress);
        var remove = harness.AddTask(admin.Id, admin.Id, "Hidden from TFA", TaskStatus.InProgress);
        var reader = new AssistantTaskReader(harness.TaskService, harness.Clock);
        var caller = AssistantServiceHarness.Caller(admin);

        await harness.TaskService.DeleteAsync(remove.Id, admin.Id, default);
        var open = await reader.OpenTasksAsync(caller, onlyMine: false, default);

        Assert.Equal([keep.Id], open.Select(task => task.Id));
    }

    [Fact]
    public async Task CommentingOnADeletedTaskIsRejected()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var admin = harness.AddUser("del-admin-5", SystemRoles.Administrator);
        var task = harness.AddTask(admin.Id, admin.Id);
        await harness.TaskService.DeleteAsync(task.Id, admin.Id, default);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            harness.CommentService.AddAsync(task.Id, new AddCommentRequest("Too late"), admin.Id, default));
    }
}
