using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Enums;

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
        Assert.Single(activity);
    }

    [Fact]
    public async Task AddComment_WhitespaceBody_Throws()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkc2");
        var task = harness.AddTask(user.Id, user.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CommentService.AddAsync(task.Id, new AddCommentRequest("   "), user.Id, CancellationToken.None));
    }

    [Fact]
    public async Task AddComment_MissingTask_Throws()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var user = harness.AddUser("tkc3");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
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
}