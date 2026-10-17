using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Services;
using TaskFlow.Infrastructure.Settings;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class ReminderServiceTests
{
    // Deadline 15 October 17:00 UTC; "now" defaults to 14 October 10:00 UTC (the second-reminder day).
    private static readonly DateTime Deadline = new(2026, 10, 15, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SecondReminderDay = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

    private static ReminderService Service(TaskTrackingServiceHarness harness, FixedTimeProvider clock)
    {
        var options = Options.Create(new ApplicationOptions { Timezone = "UTC" });
        return new ReminderService(
            harness.Db,
            new AppClock(harness.Db, clock, options, NullLogger<AppClock>.Instance),
            new ReminderSettingsService(harness.Db, clock, options),
            NullLogger<ReminderService>.Instance);
    }

    private static (Domain.Entities.User Creator, Domain.Entities.User Assignee, Domain.Entities.Task Task) SeedTask(
        TaskTrackingServiceHarness harness,
        string prefix,
        TaskStatus status = TaskStatus.InProgress)
    {
        var creator = harness.AddUser($"{prefix}-creator", Role.Manager);
        var assignee = harness.AddUser($"{prefix}-assignee");
        var task = harness.AddTask(assignee.Id, creator.Id, title: "Prepare Monthly Report", status: status, deadline: Deadline);
        harness.AddRecipient(task.Id, assignee.Id);
        return (creator, assignee, task);
    }

    [Fact]
    public async Task Assignee_creator_and_additional_recipient_each_get_one_notification()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var (creator, assignee, task) = SeedTask(harness, "rs-all");
        var extra = harness.AddUser("rs-all-extra");
        harness.AddRecipient(task.Id, extra.Id);

        var written = await Service(harness, new FixedTimeProvider(SecondReminderDay)).GenerateDueAsync();

        Assert.Equal(3, written);
        var recipients = await harness.Db.Notifications.Where(n => n.TaskId == task.Id).Select(n => n.UserId).ToListAsync();
        Assert.Equal(new[] { assignee.Id, creator.Id, extra.Id }.Order(), recipients.Order());
    }

    [Fact]
    public async Task Creator_who_is_also_a_recipient_is_reminded_once()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var (creator, _, task) = SeedTask(harness, "rs-dup");
        harness.AddRecipient(task.Id, creator.Id);

        await Service(harness, new FixedTimeProvider(SecondReminderDay)).GenerateDueAsync();

        Assert.Equal(1, await harness.Db.Notifications.CountAsync(n => n.UserId == creator.Id));
    }

    [Fact]
    public async Task Inactive_and_deleted_recipients_are_skipped()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var (_, assignee, task) = SeedTask(harness, "rs-inactive");
        var inactive = harness.AddUser("rs-inactive-extra", isActive: false);
        var deleted = harness.AddUser("rs-deleted-extra");
        deleted.IsDeleted = true;
        harness.Db.SaveChanges();
        harness.AddRecipient(task.Id, inactive.Id);
        harness.AddRecipient(task.Id, deleted.Id);

        await Service(harness, new FixedTimeProvider(SecondReminderDay)).GenerateDueAsync();

        var recipients = await harness.Db.Notifications.IgnoreQueryFilters().Select(n => n.UserId).ToListAsync();
        Assert.DoesNotContain(inactive.Id, recipients);
        Assert.DoesNotContain(deleted.Id, recipients);
        Assert.Contains(assignee.Id, recipients);
    }

    [Fact]
    public async Task Disabled_reminders_generate_nothing()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        SeedTask(harness, "rs-off");
        await harness.ReminderSettings.UpdateAsync(1, new UpdateReminderSettingsRequest(false, true, 2, 1, ReminderFrequency.Daily, false));

        var written = await Service(harness, new FixedTimeProvider(SecondReminderDay)).GenerateDueAsync();

        Assert.Equal(0, written);
        Assert.Empty(await harness.Db.Notifications.ToListAsync());
        Assert.Empty(await harness.Db.ReminderHistory.ToListAsync());
    }

    [Fact]
    public async Task Email_enabled_without_delivery_still_generates_in_app_reminders()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        SeedTask(harness, "rs-email");
        await harness.ReminderSettings.UpdateAsync(1, new UpdateReminderSettingsRequest(true, true, 2, 1, ReminderFrequency.Daily, true));

        var written = await Service(harness, new FixedTimeProvider(SecondReminderDay)).GenerateDueAsync();

        Assert.Equal(2, written);
    }

    [Fact]
    public async Task History_and_notification_record_the_reminder()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var (_, assignee, task) = SeedTask(harness, "rs-history");

        await Service(harness, new FixedTimeProvider(SecondReminderDay)).GenerateDueAsync();

        var history = await harness.Db.ReminderHistory.SingleAsync(row => row.RecipientUserId == assignee.Id);
        Assert.Equal(task.Id, history.TaskId);
        Assert.Equal(ReminderType.SecondReminder, history.ReminderType);
        Assert.Equal(ReminderStatus.Sent, history.Status);
        Assert.Equal(new DateTime(2026, 10, 14), history.ScheduledFor);
        Assert.Equal(SecondReminderDay, history.ProcessedAt);
        Assert.Equal(0, history.DeliveryAttempts);

        var notification = await harness.Db.Notifications.SingleAsync(n => n.UserId == assignee.Id);
        Assert.Equal(NotificationType.DeadlineReminder, notification.Type);
        Assert.Equal(task.Id, notification.TaskId);
        Assert.False(notification.IsRead);
        Assert.Contains("Prepare Monthly Report", notification.Message);
        Assert.Contains("due in 1 day", notification.Message);
    }

    [Fact]
    public async Task Due_today_message_says_due_today()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var (_, assignee, _) = SeedTask(harness, "rs-today");

        await Service(harness, new FixedTimeProvider(new DateTime(2026, 10, 15, 9, 0, 0))).GenerateDueAsync();

        var notification = await harness.Db.Notifications.SingleAsync(n => n.UserId == assignee.Id);
        Assert.Equal(NotificationType.DeadlineReminder, notification.Type);
        Assert.Contains("is due today", notification.Message);
    }

    [Fact]
    public async Task Overdue_reminder_uses_the_overdue_type()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var (_, assignee, task) = SeedTask(harness, "rs-overdue");

        await Service(harness, new FixedTimeProvider(new DateTime(2026, 10, 17, 9, 0, 0))).GenerateDueAsync();

        var notification = await harness.Db.Notifications.SingleAsync(n => n.UserId == assignee.Id);
        Assert.Equal(NotificationType.Overdue, notification.Type);
        Assert.Equal(task.Id, notification.TaskId);
        Assert.StartsWith("Overdue:", notification.Message);
    }

    [Theory]
    [InlineData(TaskStatus.Completed)]
    [InlineData(TaskStatus.Cancelled)]
    public async Task Finished_tasks_get_no_reminders(TaskStatus status)
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        SeedTask(harness, $"rs-{status}", status);

        var written = await Service(harness, new FixedTimeProvider(SecondReminderDay)).GenerateDueAsync();

        Assert.Equal(0, written);
    }

    [Fact]
    public async Task Deleted_tasks_get_no_reminders()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var (_, _, task) = SeedTask(harness, "rs-deleted-task");
        task.IsDeleted = true;
        harness.Db.SaveChanges();

        var written = await Service(harness, new FixedTimeProvider(SecondReminderDay)).GenerateDueAsync();

        Assert.Equal(0, written);
    }

    [Fact]
    public async Task Long_overdue_tasks_keep_receiving_overdue_reminders()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var creator = harness.AddUser("rs-old-creator", Role.Manager);
        var assignee = harness.AddUser("rs-old-assignee");
        var task = harness.AddTask(assignee.Id, creator.Id, deadline: Deadline.AddDays(-500));
        harness.AddRecipient(task.Id, assignee.Id);

        await Service(harness, new FixedTimeProvider(SecondReminderDay)).GenerateDueAsync();

        var notification = await harness.Db.Notifications.SingleAsync(n => n.UserId == assignee.Id);
        Assert.Equal(NotificationType.Overdue, notification.Type);
    }
}
