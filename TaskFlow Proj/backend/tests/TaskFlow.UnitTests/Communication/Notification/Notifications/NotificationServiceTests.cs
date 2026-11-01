using TaskFlow.Api.Dtos.Communication.Notification;
using TaskFlow.Api.Models.Enums;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests.Communication.Notification.Notifications;

public class NotificationServiceTests
{
    [Fact]
    public async Task List_ReturnsOnlyCallersRows_NewestFirst_WithTaskLink()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var owner = harness.AddUser("notif-owner");
        var other = harness.AddUser("notif-other");
        var task = harness.AddTask(owner.Id, owner.Id, title: "Linked task");

        await harness.NotificationPublisher.PublishAsync(owner.Id, task.Id, NotificationType.TaskAssigned, "first");
        await harness.NotificationPublisher.PublishAsync(owner.Id, task.Id, NotificationType.Reassigned, "second");
        await harness.NotificationPublisher.PublishAsync(other.Id, task.Id, NotificationType.TaskAssigned, "other user");

        var page = await harness.NotificationService.ListAsync(owner.Id, new NotificationFilter(), CancellationToken.None);

        Assert.Equal(2, page.Total);
        Assert.Equal(new[] { "second", "first" }, page.Items.Select(item => item.Message).ToArray());
        Assert.All(page.Items, item =>
        {
            Assert.Equal(task.Id, item.TaskId);
            Assert.Equal(task.TaskCode, item.TaskCode);
            Assert.False(item.IsRead);
        });
    }

    [Fact]
    public async Task List_AppliesTypeAndReadFilters()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("notif-filter");
        var task = harness.AddTask(user.Id, user.Id);

        await harness.NotificationPublisher.PublishAsync(user.Id, task.Id, NotificationType.TaskAssigned, "assigned");
        await harness.NotificationPublisher.PublishAsync(user.Id, task.Id, NotificationType.Reassigned, "reassigned");

        var assignedOnly = await harness.NotificationService.ListAsync(
            user.Id,
            new NotificationFilter(Type: NotificationType.TaskAssigned),
            CancellationToken.None);
        var assigned = Assert.Single(assignedOnly.Items);
        Assert.Equal("assigned", assigned.Message);
        Assert.Equal(nameof(NotificationType.TaskAssigned), assigned.Type);

        await harness.NotificationService.MarkReadAsync(user.Id, assigned.Id, CancellationToken.None);

        var unread = await harness.NotificationService.ListAsync(
            user.Id,
            new NotificationFilter(IsRead: false),
            CancellationToken.None);
        Assert.Equal("reassigned", Assert.Single(unread.Items).Message);

        var read = await harness.NotificationService.ListAsync(
            user.Id,
            new NotificationFilter(IsRead: true),
            CancellationToken.None);
        Assert.Equal("assigned", Assert.Single(read.Items).Message);
    }

    [Fact]
    public async Task UnreadCount_CountsOnlyUnreadOwnedRows()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("notif-count");
        var other = harness.AddUser("notif-count-other");

        await harness.NotificationPublisher.PublishAsync(user.Id, null, NotificationType.System, "one");
        await harness.NotificationPublisher.PublishAsync(user.Id, null, NotificationType.System, "two");
        await harness.NotificationPublisher.PublishAsync(other.Id, null, NotificationType.System, "other");

        Assert.Equal(2, await harness.NotificationService.GetUnreadCountAsync(user.Id, CancellationToken.None));

        var page = await harness.NotificationService.ListAsync(user.Id, new NotificationFilter(), CancellationToken.None);
        Assert.True(await harness.NotificationService.MarkReadAsync(user.Id, page.Items.First().Id, CancellationToken.None));

        Assert.Equal(1, await harness.NotificationService.GetUnreadCountAsync(user.Id, CancellationToken.None));
        Assert.Equal(1, await harness.NotificationService.GetUnreadCountAsync(other.Id, CancellationToken.None));
    }

    [Fact]
    public async Task MarkRead_IsIdempotentAndScopedToOwner()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var owner = harness.AddUser("notif-mark-owner");
        var other = harness.AddUser("notif-mark-other");

        await harness.NotificationPublisher.PublishAsync(owner.Id, null, NotificationType.System, "mine");
        var page = await harness.NotificationService.ListAsync(owner.Id, new NotificationFilter(), CancellationToken.None);
        var id = page.Items.Single().Id;

        Assert.True(await harness.NotificationService.MarkReadAsync(owner.Id, id, CancellationToken.None));
        Assert.True(await harness.NotificationService.MarkReadAsync(owner.Id, id, CancellationToken.None));
        Assert.Equal(0, await harness.NotificationService.GetUnreadCountAsync(owner.Id, CancellationToken.None));

        Assert.False(await harness.NotificationService.MarkReadAsync(other.Id, id, CancellationToken.None));
    }

    [Fact]
    public async Task MarkAllRead_ClearsOwnedCountOnly()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("notif-all");
        var other = harness.AddUser("notif-all-other");

        await harness.NotificationPublisher.PublishAsync(user.Id, null, NotificationType.System, "a");
        await harness.NotificationPublisher.PublishAsync(user.Id, null, NotificationType.System, "b");
        await harness.NotificationPublisher.PublishAsync(other.Id, null, NotificationType.System, "c");

        await harness.NotificationService.MarkAllReadAsync(user.Id, CancellationToken.None);

        Assert.Equal(0, await harness.NotificationService.GetUnreadCountAsync(user.Id, CancellationToken.None));
        Assert.Equal(1, await harness.NotificationService.GetUnreadCountAsync(other.Id, CancellationToken.None));
    }

    [Fact]
    public async Task List_DropsTaskLinkWhenTaskIsSoftDeleted()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("notif-deleted");
        var task = harness.AddTask(user.Id, user.Id);

        await harness.NotificationPublisher.PublishAsync(user.Id, task.Id, NotificationType.TaskAssigned, "linked");
        await harness.NotificationPublisher.PublishAsync(user.Id, null, NotificationType.System, "unlinked");

        task.IsDeleted = true;
        await harness.Db.SaveChangesAsync();

        var page = await harness.NotificationService.ListAsync(user.Id, new NotificationFilter(), CancellationToken.None);

        var linked = page.Items.Single(item => item.Message == "linked");
        Assert.Null(linked.TaskId);
        Assert.Null(linked.TaskCode);

        var unlinked = page.Items.Single(item => item.Message == "unlinked");
        Assert.Null(unlinked.TaskId);
        Assert.Null(unlinked.TaskCode);
    }

    [Fact]
    public async Task List_ClampsInvalidPaging()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("notif-paging");

        await harness.NotificationPublisher.PublishAsync(user.Id, null, NotificationType.System, "only");

        var page = await harness.NotificationService.ListAsync(
            user.Id,
            new NotificationFilter(Page: 0, PageSize: 1000),
            CancellationToken.None);

        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Single(page.Items);
    }
}
