using TaskFlow.Api.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Tasks;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.UnitTests;

public class TaskCommentServiceTests
{
    [Fact]
    public async Task AddComment_AppendsImmutableCommentAndActivity()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkc1");
        var task = harness.AddTask(user.Id, user.Id);

        var comment = await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("  Nice work  "), user.Id, CancellationToken.None);

        Assert.NotEqual(0, comment.Id);
        Assert.Equal("Nice work", comment.Body);

        var comments = await harness.CommentService.GetForTaskAsync(task.Id, CancellationToken.None);
        var stored = Assert.Single(comments);
        Assert.Equal(user.FullName, stored.AuthorName);

        var activity = await harness.ActivityService.GetForTaskAsync(
            task.Id,
            new ActivityFilter(EventType: TaskActivityEventType.CommentAdded),
            CancellationToken.None);
        Assert.Equal("Nice work", Assert.Single(activity).NewValue);
    }

    [Fact]
    public async Task AddComment_WhitespaceBody_Throws()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkc2");
        var task = harness.AddTask(user.Id, user.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            harness.CommentService.AddAsync(task.Id, new AddCommentRequest("   "), user.Id, CancellationToken.None));
    }

    [Fact]
    public async Task AddComment_MissingTask_Throws()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkc3");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            harness.CommentService.AddAsync(9999, new AddCommentRequest("Hello"), user.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetForTask_ReturnsChronologicalComments()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkc4");
        var task = harness.AddTask(user.Id, user.Id);

        await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("First"), user.Id, CancellationToken.None);
        await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("Second"), user.Id, CancellationToken.None);

        var comments = (await harness.CommentService.GetForTaskAsync(task.Id, CancellationToken.None)).ToList();

        Assert.Equal(2, comments.Count);
        Assert.Equal("First", comments[0].Body);
        Assert.Equal("Second", comments[1].Body);
    }
    [Fact]
    public async Task GetForTask_ReturnsOnlyThatTasksComments()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkc5");
        var taskA = harness.AddTask(user.Id, user.Id, "Task A");
        var taskB = harness.AddTask(user.Id, user.Id, "Task B");

        await harness.CommentService.AddAsync(taskA.Id, new AddCommentRequest("On A"), user.Id, CancellationToken.None);
        await harness.CommentService.AddAsync(taskB.Id, new AddCommentRequest("On B"), user.Id, CancellationToken.None);

        var comments = await harness.CommentService.GetForTaskAsync(taskA.Id, CancellationToken.None);

        Assert.Equal("On A", Assert.Single(comments).Body);
    }

    [Fact]
    public async Task UpdateComment_AuthorEdits_MarksEditedAndLogsActivity()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkc6");
        var task = harness.AddTask(user.Id, user.Id);
        var comment = await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("Old"), user.Id, CancellationToken.None);

        var updated = await harness.CommentService.UpdateAsync(task.Id, comment.Id, new AddCommentRequest("  New  "), user.Id, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Body);
        Assert.NotNull(updated.EditedAt);
        var listed = Assert.Single(await harness.CommentService.GetForTaskAsync(task.Id, CancellationToken.None));
        Assert.Equal("New", listed.Body);
        var activity = await harness.ActivityService.GetForTaskAsync(
            task.Id, new ActivityFilter(EventType: TaskActivityEventType.CommentEdited), CancellationToken.None);
        var edited = Assert.Single(activity);
        Assert.Equal("Old", edited.OldValue);
        Assert.Equal("New", edited.NewValue);
    }

    [Fact]
    public async Task UpdateComment_NonAuthor_IsRejectedAndTextUnchanged()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var author = harness.AddUser("tkc7");
        var other = harness.AddUser("tkc8");
        var task = harness.AddTask(author.Id, author.Id);
        var comment = await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("Mine"), author.Id, CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            harness.CommentService.UpdateAsync(task.Id, comment.Id, new AddCommentRequest("Hijack"), other.Id, CancellationToken.None));

        var listed = Assert.Single(await harness.CommentService.GetForTaskAsync(task.Id, CancellationToken.None));
        Assert.Equal("Mine", listed.Body);
    }

    [Fact]
    public async Task UpdateComment_EmptyBody_Throws()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkc9");
        var task = harness.AddTask(user.Id, user.Id);
        var comment = await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("Keep"), user.Id, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            harness.CommentService.UpdateAsync(task.Id, comment.Id, new AddCommentRequest("  "), user.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteComment_AuthorDeletes_RemovesFromListAndLogsActivity()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkc10");
        var task = harness.AddTask(user.Id, user.Id);
        var comment = await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("Bye"), user.Id, CancellationToken.None);

        Assert.True(await harness.CommentService.DeleteAsync(task.Id, comment.Id, user.Id, CancellationToken.None));

        Assert.Empty(await harness.CommentService.GetForTaskAsync(task.Id, CancellationToken.None));
        var activity = await harness.ActivityService.GetForTaskAsync(
            task.Id, new ActivityFilter(EventType: TaskActivityEventType.CommentDeleted), CancellationToken.None);
        Assert.Equal("Bye", Assert.Single(activity).OldValue);
        Assert.False(await harness.CommentService.DeleteAsync(task.Id, comment.Id, user.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteComment_NonAuthor_IsRejectedAndCommentRemains()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var author = harness.AddUser("tkc11");
        var other = harness.AddUser("tkc12");
        var task = harness.AddTask(author.Id, author.Id);
        var comment = await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("Stay"), author.Id, CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            harness.CommentService.DeleteAsync(task.Id, comment.Id, other.Id, CancellationToken.None));

        Assert.Single(await harness.CommentService.GetForTaskAsync(task.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CommentChanges_NotifyAssignedMembersAndTheActor_WithNotificationAndInboxMail()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var author = harness.AddUser("tkc13");
        var assignee = harness.AddUser("tkc14");
        var extra = harness.AddUser("tkc15");
        var outsider = harness.AddUser("tkc16");
        var task = harness.AddTask(assignee.Id, author.Id, "Notify task");
        harness.AddRecipient(task.Id, author.Id);
        harness.AddRecipient(task.Id, extra.Id);

        var comment = await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("Hello"), author.Id, CancellationToken.None);
        await harness.CommentService.UpdateAsync(task.Id, comment.Id, new AddCommentRequest("Hello again"), author.Id, CancellationToken.None);
        await harness.CommentService.DeleteAsync(task.Id, comment.Id, author.Id, CancellationToken.None);

        foreach (var member in new[] { assignee, extra, author })
        {
            var notifications = await harness.Db.Notifications.Where(n => n.UserId == member.Id && n.Type == NotificationType.TaskComment).ToListAsync();
            Assert.Equal(3, notifications.Count);
            Assert.All(notifications, n => Assert.Equal(task.Id, n.TaskId));
            Assert.Equal(3, await harness.Db.MailboxItems.CountAsync(item => item.OwnerUserId == member.Id && item.Folder == MailFolder.Inbox));
        }

        var texts = await harness.Db.Notifications.Where(n => n.UserId == assignee.Id).OrderBy(n => n.Id).Select(n => n.Message).ToListAsync();
        Assert.Contains("added a comment on \"Notify task\": \"Hello\"", texts[0]);
        Assert.Contains("\"Hello\" → \"Hello again\"", texts[1]);
        Assert.Contains("deleted a comment on \"Notify task\": \"Hello again\"", texts[2]);

        var mails = await harness.Db.MailMessages.Where(m => m.TaskId == task.Id).OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(["Comment added: Notify task", "Comment edited: Notify task", "Comment deleted: Notify task"], mails.Select(m => m.Subject));
        Assert.Contains("Hello", mails[0].Body);
        Assert.Contains("Previous:", mails[1].Body);
        Assert.Contains("Updated:", mails[1].Body);
        Assert.Contains("Hello again", mails[2].Body);

        Assert.Empty(await harness.Db.Notifications.Where(n => n.UserId == outsider.Id).ToListAsync());
        Assert.Empty(await harness.Db.MailboxItems.Where(item => item.OwnerUserId == outsider.Id).ToListAsync());
    }

    [Fact]
    public async Task AddComment_ActorNotOnTheTask_StillGetsNotificationAndMail()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var actor = harness.AddUser("tkc21");
        var assignee = harness.AddUser("tkc22");
        var task = harness.AddTask(assignee.Id, assignee.Id, "Outside task");

        await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("Visiting"), actor.Id, CancellationToken.None);

        Assert.Single(await harness.Db.Notifications.Where(n => n.UserId == actor.Id).ToListAsync());
        Assert.Single(await harness.Db.MailboxItems.Where(item => item.OwnerUserId == actor.Id).ToListAsync());
        Assert.Single(await harness.Db.Notifications.Where(n => n.UserId == assignee.Id).ToListAsync());
    }

    [Fact]
    public async Task AddComment_InAppDisabled_SavesCommentWithoutNotifications()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var author = harness.AddUser("tkc17");
        var assignee = harness.AddUser("tkc18");
        var task = harness.AddTask(assignee.Id, author.Id);
        harness.SetSetting(TaskFlow.Api.Common.Constants.SettingKeys.NotificationInAppEnabled, "false");

        await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("Quiet"), author.Id, CancellationToken.None);

        Assert.Single(await harness.CommentService.GetForTaskAsync(task.Id, CancellationToken.None));
        Assert.Empty(await harness.Db.Notifications.ToListAsync());
        Assert.Single(await harness.Db.MailboxItems.Where(item => item.OwnerUserId == assignee.Id).ToListAsync());
    }

    [Fact]
    public async Task AddComment_MailDisabled_SendsNoInboxMail()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var author = harness.AddUser("tkc19");
        var assignee = harness.AddUser("tkc20");
        var task = harness.AddTask(assignee.Id, author.Id);
        harness.SetSetting(TaskFlow.Api.Common.Constants.SettingKeys.NotificationMailEnabled, "false");

        await harness.CommentService.AddAsync(task.Id, new AddCommentRequest("No mail"), author.Id, CancellationToken.None);

        Assert.Single(await harness.Db.Notifications.Where(n => n.UserId == assignee.Id).ToListAsync());
        Assert.Empty(await harness.Db.MailboxItems.ToListAsync());
    }

}

public class TaskActivityServiceTests
{
    [Fact]
    public async Task GetForTask_FiltersByEventTypeActorDateRangeAndSearch()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tka1");
        var other = harness.AddUser("tka2");
        var task = harness.AddTask(user.Id, user.Id);

        await harness.ActivityService.AppendAsync(task.Id, TaskActivityEventType.Assigned, user.Id, "Assigned to analyst", cancellationToken: CancellationToken.None);
        await harness.ActivityService.AppendAsync(task.Id, TaskActivityEventType.DeadlineChanged, other.Id, "Deadline moved to next week", cancellationToken: CancellationToken.None);
        await harness.ActivityService.AppendAsync(task.Id, TaskActivityEventType.CommentAdded, user.Id, "Comment added", cancellationToken: CancellationToken.None);

        var now = DateTime.UtcNow;

        var byEventType = await harness.ActivityService.GetForTaskAsync(
            task.Id,
            new ActivityFilter(EventType: TaskActivityEventType.DeadlineChanged),
            CancellationToken.None);
        Assert.Single(byEventType);

        var byActor = await harness.ActivityService.GetForTaskAsync(
            task.Id,
            new ActivityFilter(ActorUserId: other.Id),
            CancellationToken.None);
        Assert.Single(byActor);

        var byDateRange = await harness.ActivityService.GetForTaskAsync(
            task.Id,
            new ActivityFilter(From: now.AddDays(-1), To: now.AddDays(1)),
            CancellationToken.None);
        Assert.Equal(3, byDateRange.Count);

        var bySearch = await harness.ActivityService.GetForTaskAsync(
            task.Id,
            new ActivityFilter(Search: "moved"),
            CancellationToken.None);
        Assert.Single(bySearch);
    }

    [Fact]
    public async Task GetForTask_OrdersOldestFirst()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tka3");
        var task = harness.AddTask(user.Id, user.Id);

        await harness.ActivityService.AppendAsync(task.Id, TaskActivityEventType.Created, user.Id, "Task created", cancellationToken: CancellationToken.None);
        await harness.ActivityService.AppendAsync(task.Id, TaskActivityEventType.Assigned, user.Id, "Assignment set", cancellationToken: CancellationToken.None);

        var entries = (await harness.ActivityService.GetForTaskAsync(task.Id, new ActivityFilter(), CancellationToken.None)).ToList();

        Assert.Equal(2, entries.Count);
        Assert.Equal("Task created", entries[0].Message);
        Assert.Equal("Assignment set", entries[1].Message);
    }
    private static async Task<(int First, int Second, int Third)> SeedHoursAsync(TaskTrackingServiceHarness harness, int taskId, int userId)
    {
        // 2h, then 3h more (5h total), then 4h more (9h total).
        await harness.ActivityService.AppendAsync(taskId, TaskActivityEventType.HoursLogged, userId, "Worked hours logged: 2h (total)", "0", "2", CancellationToken.None);
        await harness.ActivityService.AppendAsync(taskId, TaskActivityEventType.HoursLogged, userId, "Worked hours logged: 5h (total)", "2.0", "5", CancellationToken.None);
        await harness.ActivityService.AppendAsync(taskId, TaskActivityEventType.HoursLogged, userId, "Worked hours logged: 9h (total)", "5.0", "9", CancellationToken.None);
        var ids = await harness.Db.TaskActivityLogs
            .Where(log => log.TaskId == taskId && log.EventType == TaskActivityEventType.HoursLogged)
            .OrderBy(log => log.Id).Select(log => log.Id).ToListAsync();
        var task = await harness.Db.Tasks.SingleAsync(candidate => candidate.Id == taskId);
        task.ActualHours = 9;
        await harness.Db.SaveChangesAsync();
        return (ids[0], ids[1], ids[2]);
    }

    [Fact]
    public async Task UpdateHoursEntry_ChangesTheEntryAndRebuildsTotals()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var admin = harness.AddUser("tka4", SystemRoles.Administrator);
        var task = harness.AddTask(admin.Id, admin.Id);
        var (_, second, _) = await SeedHoursAsync(harness, task.Id, admin.Id);

        Assert.True(await harness.ActivityService.UpdateHoursEntryAsync(task.Id, second, 1m, admin.Id, CancellationToken.None));

        var entries = await harness.Db.TaskActivityLogs.AsNoTracking()
            .Where(log => log.TaskId == task.Id && log.EventType == TaskActivityEventType.HoursLogged)
            .OrderBy(log => log.Id).ToListAsync();
        Assert.Equal(["2", "3", "7"], entries.Select(entry => entry.NewValue));
        Assert.Equal(["0", "2", "3"], entries.Select(entry => entry.OldValue));
        Assert.Equal(7m, (await harness.Db.Tasks.AsNoTracking().SingleAsync(candidate => candidate.Id == task.Id)).ActualHours);
        var audit = Assert.Single(await harness.Db.TaskActivityLogs.Where(log => log.EventType == TaskActivityEventType.Administrative).ToListAsync());
        Assert.Contains("from 3h to 1h", audit.Message);
    }

    [Fact]
    public async Task DeleteHoursEntry_RemovesItAndRebuildsTotals()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var admin = harness.AddUser("tka5", SystemRoles.Administrator);
        var task = harness.AddTask(admin.Id, admin.Id);
        var (first, _, _) = await SeedHoursAsync(harness, task.Id, admin.Id);

        Assert.True(await harness.ActivityService.DeleteHoursEntryAsync(task.Id, first, admin.Id, CancellationToken.None));

        var entries = await harness.Db.TaskActivityLogs.AsNoTracking()
            .Where(log => log.TaskId == task.Id && log.EventType == TaskActivityEventType.HoursLogged)
            .OrderBy(log => log.Id).ToListAsync();
        Assert.Equal(["3", "7"], entries.Select(entry => entry.NewValue));
        Assert.Equal(7m, (await harness.Db.Tasks.AsNoTracking().SingleAsync(candidate => candidate.Id == task.Id)).ActualHours);
        Assert.Contains(await harness.Db.TaskActivityLogs.ToListAsync(), log => log.EventType == TaskActivityEventType.Administrative);
    }

    [Fact]
    public async Task HoursEntry_OtherEntryOrNonPositiveHours_IsRejectedOrNotFound()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var admin = harness.AddUser("tka6", SystemRoles.Administrator);
        var task = harness.AddTask(admin.Id, admin.Id);
        var (first, _, _) = await SeedHoursAsync(harness, task.Id, admin.Id);
        await harness.ActivityService.AppendAsync(task.Id, TaskActivityEventType.Created, admin.Id, "Created", cancellationToken: CancellationToken.None);
        var notHours = await harness.Db.TaskActivityLogs.Where(log => log.EventType == TaskActivityEventType.Created).Select(log => log.Id).FirstAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            harness.ActivityService.UpdateHoursEntryAsync(task.Id, first, 0m, admin.Id, CancellationToken.None));
        Assert.False(await harness.ActivityService.UpdateHoursEntryAsync(task.Id, notHours, 2m, admin.Id, CancellationToken.None));
        Assert.False(await harness.ActivityService.DeleteHoursEntryAsync(task.Id, notHours, admin.Id, CancellationToken.None));
        Assert.False(await harness.ActivityService.DeleteHoursEntryAsync(task.Id, 99999, admin.Id, CancellationToken.None));
    }

}

public class NotificationPublisherTests
{
    [Fact]
    public async Task PublishAsync_InsertsUnreadNotification()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkn1");

        await harness.NotificationPublisher.PublishAsync(user.Id, taskId: null, NotificationType.System, "Hello user", CancellationToken.None);

        var notification = await harness.Db.Notifications.SingleAsync(candidate => candidate.UserId == user.Id);

        Assert.False(notification.IsRead);
        Assert.Equal(NotificationType.System, notification.Type);
        Assert.Equal("Hello user", notification.Message);
    }

    [Fact]
    public async Task PublishAsync_StoresTaskLink()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkn2");
        var task = harness.AddTask(user.Id, user.Id);

        await harness.NotificationPublisher.PublishAsync(user.Id, task.Id, NotificationType.TaskAssigned, "New task", CancellationToken.None);

        var notification = await harness.Db.Notifications.SingleAsync(candidate => candidate.TaskId == task.Id);
        Assert.Equal(NotificationType.TaskAssigned, notification.Type);
    }

    [Fact]
    public async Task PublishAsync_SkipsWhenInAppNotificationsAreDisabled()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkn3");
        harness.SetSetting(TaskFlow.Api.Common.Constants.SettingKeys.NotificationInAppEnabled, "false");

        await harness.NotificationPublisher.PublishAsync(user.Id, taskId: null, NotificationType.System, "Muted", CancellationToken.None);

        Assert.False(await harness.Db.Notifications.AnyAsync(candidate => candidate.UserId == user.Id));
    }

    [Fact]
    public async Task PublishAsync_WritesWhenInAppNotificationsAreEnabled()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkn4");
        harness.SetSetting(TaskFlow.Api.Common.Constants.SettingKeys.NotificationInAppEnabled, "true");

        await harness.NotificationPublisher.PublishAsync(user.Id, taskId: null, NotificationType.System, "Loud", CancellationToken.None);

        Assert.True(await harness.Db.Notifications.AnyAsync(candidate => candidate.UserId == user.Id));
    }

    [Fact]
    public async Task PublishAsync_DefaultsToEnabledWhenTheSettingRowIsMissing()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkn5");

        await harness.NotificationPublisher.PublishAsync(user.Id, taskId: null, NotificationType.System, "Default", CancellationToken.None);

        Assert.True(await harness.Db.Notifications.AnyAsync(candidate => candidate.UserId == user.Id));
    }
}