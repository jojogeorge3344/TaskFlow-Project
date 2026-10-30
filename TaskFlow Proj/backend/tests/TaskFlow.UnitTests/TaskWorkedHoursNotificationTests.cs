using TaskFlow.Api.Services.Monitoring.TaskManagement;
using TaskFlow.Api.Dtos.Monitoring.TaskManagement;
using TaskFlow.Api.Services.Interfaces.Communication.Mail;
using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Security;
using TaskFlow.Infrastructure.Services;

namespace TaskFlow.UnitTests;

public class TaskWorkedHoursNotificationTests
{
    private static UpdateTaskRequest Request(TaskFlow.Api.Models.Entities.Task task, decimal? hours) =>
        new(task.Title, task.Description, task.Priority, task.StartDate, task.Deadline, task.EstimatedHours, hours);

    [Fact]
    public async Task Update_AddedHours_NotifiesAssigneeRecipientsAndTheActor()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("wh-mgr", SystemRoles.Manager);
        var assignee = harness.AddUser("wh-assignee");
        var recipient = harness.AddUser("wh-recipient");
        var task = harness.AddTask(assignee.Id, manager.Id);
        harness.AddRecipient(task.Id, recipient.Id);
        harness.AddRecipient(task.Id, manager.Id);

        await harness.TaskService.UpdateAsync(task.Id, Request(task, 3m), manager.Id, SystemRoles.Manager, CancellationToken.None);

        var notified = await harness.Db.Notifications.Select(n => n.UserId).ToListAsync();
        Assert.Equivalent(new[] { assignee.Id, recipient.Id, manager.Id }, notified);
        var added = (await harness.Db.Notifications.FirstAsync()).Message;
        Assert.Contains("added 3h", added);
        Assert.Contains("Total worked now: 3h of 8h estimated (5h remaining)", added);
        Assert.All(await harness.Db.MailMessages.ToListAsync(), mail => Assert.Contains("Total worked now: 3h", mail.Body));
        Assert.Equal(3, await harness.Db.MailboxItems.CountAsync(item => item.Folder == MailFolder.Inbox));
    }

    [Fact]
    public async Task Update_ClearedHours_SaysTheHoursWereCleared()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("wh-mgr2", SystemRoles.Manager);
        var assignee = harness.AddUser("wh-assignee2");
        var task = harness.AddTask(assignee.Id, manager.Id);
        await harness.TaskService.UpdateAsync(task.Id, Request(task, 4m), manager.Id, SystemRoles.Manager, CancellationToken.None);
        harness.Db.Notifications.RemoveRange(harness.Db.Notifications);
        await harness.Db.SaveChangesAsync();

        await harness.TaskService.UpdateAsync(task.Id, Request(task, 0m), manager.Id, SystemRoles.Manager, CancellationToken.None);

        var message = (await harness.Db.Notifications.SingleAsync()).Message;
        Assert.Contains("cleared the worked hours", message);
        Assert.Contains("Total worked now: 0h", message);
        Assert.Equal(0m, (await harness.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == task.Id)).ActualHours);
    }

    [Fact]
    public async Task Update_UnchangedHours_SendsNothing()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("wh-mgr3", SystemRoles.Manager);
        var assignee = harness.AddUser("wh-assignee3");
        var task = harness.AddTask(assignee.Id, manager.Id);

        await harness.TaskService.UpdateAsync(task.Id, Request(task, null), manager.Id, SystemRoles.Manager, CancellationToken.None);

        Assert.Empty(await harness.Db.Notifications.ToListAsync());
        Assert.Empty(await harness.Db.MailboxItems.ToListAsync());
    }

    [Fact]
    public async Task Update_WhenMailFails_StillSavesTheHours()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var manager = harness.AddUser("wh-mgr4", SystemRoles.Manager);
        var assignee = harness.AddUser("wh-assignee4");
        var task = harness.AddTask(assignee.Id, manager.Id);
        var service = TestServices.Tasks(
            harness.Db,
            harness.ActivityService,
            harness.NotificationPublisher,
            TimeProvider.System,
            harness.ReminderSettings,
            harness.Clock,
            new ThrowingMail(),
            NullLogger<TaskService>.Instance);

        var details = await service.UpdateAsync(task.Id, Request(task, 2m), manager.Id, SystemRoles.Manager, CancellationToken.None);

        Assert.Equal(2m, details!.ActualHours);
        Assert.Equal(2m, (await harness.Db.Tasks.AsNoTracking().SingleAsync(t => t.Id == task.Id)).ActualHours);
    }

    private sealed class ThrowingMail : IMailService
    {
        public Task<MailItemDetailDto?> SendAsync(int callerId, SendMailRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("mail down");

        public Task<MailItemDetailDto?> CreateDraftAsync(int callerId, SaveDraftRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MailItemDetailDto?> UpdateDraftAsync(int callerId, int draftId, SaveDraftRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<MailListItemDto>> ListAsync(int callerId, MailListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MailSummaryDto> GetSummaryAsync(int callerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> GetUnreadCountAsync(int callerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MailItemDetailDto?> OpenAsync(int callerId, int itemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateFlagsAsync(int callerId, int itemId, UpdateMailFlagsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TrashAsync(int callerId, int itemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RestoreAsync(int callerId, int itemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteForeverAsync(int callerId, int itemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MailComposeDto?> GetComposeAsync(int callerId, int itemId, MailComposeMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MailContactDto>> GetContactsAsync(string? search, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
