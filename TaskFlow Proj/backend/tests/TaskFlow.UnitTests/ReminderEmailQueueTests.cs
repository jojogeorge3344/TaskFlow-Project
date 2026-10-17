using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Services;
using TaskFlow.Infrastructure.Settings;
using Task = System.Threading.Tasks.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

/// <summary>The reminder engine queues one email per generated reminder when email is enabled, in the same
/// transaction as the in-app notification.</summary>
public class ReminderEmailQueueTests
{
    // Deadline 15 October 17:00 UTC (22:30 in Asia/Kolkata); the second reminder falls on 14 October.
    private static readonly DateTime Deadline = new(2026, 10, 15, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SecondReminderDayUtc = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

    private static ReminderService Service(
        TaskTrackingServiceHarness harness,
        FixedTimeProvider clock,
        string timezone = "UTC",
        string? appBaseUrl = "http://localhost:4200")
    {
        var options = Options.Create(new ApplicationOptions { Timezone = timezone });
        var emailOptions = Options.Create(new EmailOptions { AppBaseUrl = appBaseUrl });
        return new ReminderService(
            harness.Db,
            new AppClock(harness.Db, clock, options, NullLogger<AppClock>.Instance),
            new ReminderSettingsService(harness.Db, clock, options),
            new EmailSettingsService(harness.Db, clock, emailOptions),
            emailOptions,
            NullLogger<ReminderService>.Instance);
    }

    private static void SetSetting(TaskTrackingServiceHarness harness, string key, string value)
    {
        var existing = harness.Db.ApplicationSettings.SingleOrDefault(setting => setting.Key == key);
        if (existing is null)
        {
            harness.Db.ApplicationSettings.Add(new ApplicationSetting { Key = key, Value = value, Category = "Test", UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            existing.Value = value;
        }

        harness.Db.SaveChanges();
    }

    private static void EnableEmail(TaskTrackingServiceHarness harness, string sendTime = "09:00")
    {
        SetSetting(harness, SettingKeys.NotificationEmailEnabled, "true");
        SetSetting(harness, SettingKeys.EmailSendTime, sendTime);
    }

    private static (User Creator, User Assignee, Domain.Entities.Task Task) SeedTask(TaskTrackingServiceHarness harness, string prefix)
    {
        var creator = harness.AddUser($"{prefix}-creator", Role.Manager);
        var assignee = harness.AddUser($"{prefix}-assignee");
        var task = harness.AddTask(assignee.Id, creator.Id, title: "Prepare Monthly Report", status: TaskStatus.InProgress, deadline: Deadline);
        harness.AddRecipient(task.Id, assignee.Id);
        return (creator, assignee, task);
    }

    [Fact]
    public async Task Email_disabled_queues_nothing()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        var (_, _, task) = SeedTask(harness, "eq-off");

        await Service(harness, new FixedTimeProvider(SecondReminderDayUtc)).GenerateDueAsync();

        Assert.Equal(2, await harness.Db.Notifications.CountAsync(n => n.TaskId == task.Id));
        Assert.Equal(0, await harness.Db.EmailDeliveryLogs.CountAsync());
    }

    [Fact]
    public async Task Enabled_email_queues_one_pending_email_per_recipient()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        EnableEmail(harness);
        var (creator, assignee, task) = SeedTask(harness, "eq-on");
        var extra = harness.AddUser("eq-on-extra");
        harness.AddRecipient(task.Id, extra.Id);

        await Service(harness, new FixedTimeProvider(SecondReminderDayUtc)).GenerateDueAsync();

        var emails = await harness.Db.EmailDeliveryLogs.AsNoTracking().ToListAsync();
        Assert.Equal(3, emails.Count);
        Assert.Equal(
            new[] { creator.Email, assignee.Email, extra.Email }.Order(),
            emails.Select(email => email.ToAddress).Order());
        var history = await harness.Db.ReminderHistory.AsNoTracking().ToDictionaryAsync(row => row.Id);
        Assert.All(emails, email =>
        {
            Assert.Equal(EmailKind.Reminder, email.Kind);
            Assert.Equal(EmailDeliveryStatus.Pending, email.Status);
            Assert.Equal(0, email.Attempts);
            Assert.Equal(task.Id, email.TaskId);
            Assert.Equal(Deadline, email.TaskDeadline);
            Assert.Equal("Task reminder: Prepare Monthly Report", email.Subject);
            Assert.Contains("is due in 1 day", email.Body);
            Assert.Contains($"http://localhost:4200/tasks/{task.Id}", email.Body);
            Assert.Equal(email.RecipientUserId, history[email.ReminderHistoryId!.Value].RecipientUserId);
        });
        Assert.Equal(3, await harness.Db.Notifications.CountAsync(n => n.TaskId == task.Id));
    }

    [Fact]
    public async Task Recipient_without_an_email_address_gets_only_the_in_app_notification()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        EnableEmail(harness);
        var (creator, assignee, task) = SeedTask(harness, "eq-blank");
        assignee.Email = "";
        harness.Db.SaveChanges();

        await Service(harness, new FixedTimeProvider(SecondReminderDayUtc)).GenerateDueAsync();

        Assert.Equal(1, await harness.Db.Notifications.CountAsync(n => n.UserId == assignee.Id && n.TaskId == task.Id));
        var email = await harness.Db.EmailDeliveryLogs.SingleAsync();
        Assert.Equal(creator.Id, email.RecipientUserId);
    }

    [Fact]
    public async Task Reminder_generated_at_local_midnight_waits_for_the_send_time()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        EnableEmail(harness, "09:00");
        SeedTask(harness, "eq-midnight");
        // 00:01 on 14 October in Asia/Kolkata (UTC+05:30).
        var now = new DateTime(2026, 10, 13, 18, 31, 0, DateTimeKind.Utc);

        await Service(harness, new FixedTimeProvider(now), "Asia/Kolkata").GenerateDueAsync();

        var emails = await harness.Db.EmailDeliveryLogs.AsNoTracking().ToListAsync();
        Assert.Equal(2, emails.Count);
        Assert.All(emails, email =>
        {
            // 09:00 IST on 14 October, and the end of that local day.
            Assert.Equal(new DateTime(2026, 10, 14, 3, 30, 0), email.NextAttemptAt);
            Assert.Equal(new DateTime(2026, 10, 14, 18, 30, 0), email.ExpiresAt);
        });
    }

    [Fact]
    public async Task Reminder_generated_after_the_send_time_is_due_immediately()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        EnableEmail(harness, "09:00");
        SeedTask(harness, "eq-afternoon");
        // 14:00 on 14 October in Asia/Kolkata.
        var now = new DateTime(2026, 10, 14, 8, 30, 0, DateTimeKind.Utc);

        await Service(harness, new FixedTimeProvider(now), "Asia/Kolkata").GenerateDueAsync();

        Assert.All(await harness.Db.EmailDeliveryLogs.AsNoTracking().ToListAsync(), email => Assert.Equal(now, email.NextAttemptAt));
    }

    [Fact]
    public async Task Repeated_passes_queue_each_email_once()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        EnableEmail(harness);
        SeedTask(harness, "eq-repeat");
        var clock = new FixedTimeProvider(SecondReminderDayUtc);

        for (var pass = 0; pass < 3; pass++)
        {
            await Service(harness, clock).GenerateDueAsync();
            clock.Set(SecondReminderDayUtc.AddMinutes(pass + 1));
        }

        Assert.Equal(2, await harness.Db.EmailDeliveryLogs.CountAsync());
        Assert.Equal(2, await harness.Db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Without_an_app_address_the_email_has_no_link()
    {
        await using var harness = TaskTrackingServiceHarness.Create();
        EnableEmail(harness);
        SeedTask(harness, "eq-nolink");

        await Service(harness, new FixedTimeProvider(SecondReminderDayUtc), appBaseUrl: null).GenerateDueAsync();

        Assert.All(await harness.Db.EmailDeliveryLogs.AsNoTracking().ToListAsync(), email => Assert.DoesNotContain("/tasks/", email.Body));
    }
}
