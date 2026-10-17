using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Email;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Services;
using TaskFlow.Infrastructure.Settings;
using Task = System.Threading.Tasks.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

/// <summary>Records every email it is asked to send and returns scripted results (success by default).</summary>
internal sealed class FakeEmailSender : IEmailSender
{
    private readonly Queue<EmailSendResult> _results = new();

    public List<EmailMessage> Sent { get; } = [];

    /// <summary>Runs before each send, e.g. to simulate another worker.</summary>
    public Func<Task>? BeforeSend { get; set; }

    public EmailSendResult Fallback { get; set; } = EmailSendResult.Sent;

    public void Enqueue(params EmailSendResult[] results)
    {
        foreach (var result in results)
        {
            _results.Enqueue(result);
        }
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, SmtpSettings settings, CancellationToken cancellationToken = default)
    {
        if (BeforeSend is { } hook)
        {
            BeforeSend = null;
            await hook();
        }

        Sent.Add(message);
        return _results.Count > 0 ? _results.Dequeue() : Fallback;
    }
}

public class EmailDispatcherTests
{
    // Deadline 15 October 17:00 UTC; the second reminder is generated just after midnight on 14 October and
    // its email waits for the 09:00 send time (application timezone UTC).
    private static readonly DateTime Deadline = new(2026, 10, 15, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime GeneratedAt = new(2026, 10, 14, 0, 1, 0, DateTimeKind.Utc);
    private static readonly DateTime SendTime = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly EmailSendResult ConnectionRefused = EmailSendResult.Failed("Could not connect to smtp.test:25.");

    private sealed class Fixture : IAsyncDisposable
    {
        public TaskTrackingServiceHarness Harness { get; } = TaskTrackingServiceHarness.Create();
        public FixedTimeProvider Clock { get; } = new(GeneratedAt);
        public FakeEmailSender Sender { get; } = new();
        public Domain.Entities.Task Task { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync(bool configureSmtp = true)
        {
            var fixture = new Fixture();
            fixture.Set(SettingKeys.NotificationEmailEnabled, "true");
            fixture.Set(SettingKeys.EmailSendTime, "09:00");
            if (configureSmtp)
            {
                fixture.Set(SettingKeys.EmailSmtpHost, "smtp.test");
                fixture.Set(SettingKeys.EmailSenderAddress, "taskflow@test.local");
            }

            var creator = fixture.Harness.AddUser("disp-creator", Role.Manager);
            var assignee = fixture.Harness.AddUser("disp-assignee");
            fixture.Task = fixture.Harness.AddTask(assignee.Id, creator.Id, title: "Prepare Monthly Report", status: TaskStatus.InProgress, deadline: Deadline);
            fixture.Harness.AddRecipient(fixture.Task.Id, assignee.Id);

            await fixture.Reminders().GenerateDueAsync();
            return fixture;
        }

        public AppDbContextAccessor Db => new(Harness);

        public void Set(string key, string value)
        {
            var existing = Harness.Db.ApplicationSettings.SingleOrDefault(setting => setting.Key == key);
            if (existing is null)
            {
                Harness.Db.ApplicationSettings.Add(new ApplicationSetting { Key = key, Value = value, Category = "Test", UpdatedAt = DateTime.UtcNow });
            }
            else
            {
                existing.Value = value;
            }

            Harness.Db.SaveChanges();
        }

        public ReminderService Reminders()
        {
            var options = Options.Create(new ApplicationOptions { Timezone = "UTC" });
            var emailOptions = Options.Create(new EmailOptions());
            return new ReminderService(
                Harness.Db,
                new AppClock(Harness.Db, Clock, options, NullLogger<AppClock>.Instance),
                new ReminderSettingsService(Harness.Db, Clock, options),
                new EmailSettingsService(Harness.Db, Clock, emailOptions),
                emailOptions,
                NullLogger<ReminderService>.Instance);
        }

        public EmailDispatcher Dispatcher()
        {
            var emailOptions = Options.Create(new EmailOptions { MaxAttempts = 3 });
            return new EmailDispatcher(
                Harness.Db,
                Sender,
                new EmailSettingsService(Harness.Db, Clock, emailOptions),
                Clock,
                emailOptions,
                NullLogger<EmailDispatcher>.Instance);
        }

        public async Task<int> DispatchAtAsync(DateTime utc)
        {
            Clock.Set(utc);
            return await Dispatcher().DispatchDueAsync();
        }

        public Task<List<EmailDeliveryLog>> EmailsAsync() =>
            Harness.Db.EmailDeliveryLogs.AsNoTracking().OrderBy(email => email.Id).ToListAsync();

        public ValueTask DisposeAsync() => Harness.DisposeAsync();
    }

    /// <summary>Keeps the fixture's in-app state checks short.</summary>
    private readonly record struct AppDbContextAccessor(TaskTrackingServiceHarness Harness)
    {
        public Task<int> NotificationCount() => Harness.Db.Notifications.CountAsync();

        public Task<List<ReminderHistory>> History() => Harness.Db.ReminderHistory.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task Due_emails_are_sent_and_logged_on_the_first_attempt()
    {
        await using var fixture = await Fixture.CreateAsync();

        var sent = await fixture.DispatchAtAsync(SendTime);

        Assert.Equal(2, sent);
        Assert.Equal(2, fixture.Sender.Sent.Count);
        Assert.All(await fixture.EmailsAsync(), email =>
        {
            Assert.Equal(EmailDeliveryStatus.Sent, email.Status);
            Assert.Equal(1, email.Attempts);
            Assert.Equal(SendTime, email.SentAt);
            Assert.Equal(SendTime, email.LastAttemptAt);
            Assert.Null(email.LastError);
            Assert.Null(email.NextAttemptAt);
        });
        Assert.All(await fixture.Db.History(), row => Assert.Equal(1, row.DeliveryAttempts));
    }

    [Fact]
    public async Task Emails_are_not_sent_before_the_send_time()
    {
        await using var fixture = await Fixture.CreateAsync();

        Assert.Equal(0, await fixture.DispatchAtAsync(SendTime.AddMinutes(-1)));

        Assert.Empty(fixture.Sender.Sent);
        Assert.All(await fixture.EmailsAsync(), email => Assert.Equal(EmailDeliveryStatus.Pending, email.Status));
    }

    [Fact]
    public async Task Temporary_failure_is_retried_and_then_sent()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Sender.Enqueue(ConnectionRefused, ConnectionRefused);

        Assert.Equal(0, await fixture.DispatchAtAsync(SendTime));
        var waiting = await fixture.EmailsAsync();
        Assert.All(waiting, email =>
        {
            Assert.Equal(EmailDeliveryStatus.Pending, email.Status);
            Assert.Equal(1, email.Attempts);
            Assert.Equal(ConnectionRefused.Error, email.LastError);
            Assert.Equal(SendTime.AddMinutes(5), email.NextAttemptAt);
        });

        Assert.Equal(0, await fixture.DispatchAtAsync(SendTime.AddMinutes(4)));
        Assert.Equal(2, await fixture.DispatchAtAsync(SendTime.AddMinutes(5)));

        Assert.All(await fixture.EmailsAsync(), email =>
        {
            Assert.Equal(EmailDeliveryStatus.Sent, email.Status);
            Assert.Equal(2, email.Attempts);
            Assert.Null(email.LastError);
        });
        Assert.All(await fixture.Db.History(), row => Assert.Equal(2, row.DeliveryAttempts));
    }

    [Fact]
    public async Task Third_failure_marks_the_email_failed_for_good()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Sender.Fallback = ConnectionRefused;

        await fixture.DispatchAtAsync(SendTime);
        await fixture.DispatchAtAsync(SendTime.AddMinutes(5));
        var afterSecond = await fixture.EmailsAsync();
        Assert.All(afterSecond, email => Assert.Equal(SendTime.AddMinutes(20), email.NextAttemptAt));
        await fixture.DispatchAtAsync(SendTime.AddMinutes(20));
        await fixture.DispatchAtAsync(SendTime.AddHours(2));

        Assert.Equal(6, fixture.Sender.Sent.Count);
        Assert.All(await fixture.EmailsAsync(), email =>
        {
            Assert.Equal(EmailDeliveryStatus.Failed, email.Status);
            Assert.Equal(3, email.Attempts);
            Assert.Equal(ConnectionRefused.Error, email.LastError);
            Assert.Null(email.NextAttemptAt);
        });
    }

    [Fact]
    public async Task Failures_never_touch_in_app_notifications_or_history()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Sender.Fallback = ConnectionRefused;
        var before = await fixture.Db.NotificationCount();

        foreach (var minutes in new[] { 0, 5, 20 })
        {
            await fixture.DispatchAtAsync(SendTime.AddMinutes(minutes));
        }

        Assert.Equal(2, before);
        Assert.Equal(before, await fixture.Db.NotificationCount());
        Assert.All(await fixture.Db.History(), row => Assert.Equal(ReminderStatus.Sent, row.Status));
    }

    [Fact]
    public async Task Incomplete_configuration_is_recorded_without_contacting_a_server()
    {
        await using var fixture = await Fixture.CreateAsync(configureSmtp: false);

        await fixture.DispatchAtAsync(SendTime);

        Assert.Empty(fixture.Sender.Sent);
        Assert.All(await fixture.EmailsAsync(), email =>
        {
            Assert.Equal(1, email.Attempts);
            Assert.StartsWith(EmailDispatcher.ConfigurationPrefix, email.LastError);
            Assert.Contains(EmailConfigurationCheck.MissingHost, email.LastError);
        });
        Assert.Equal(2, await fixture.Db.NotificationCount());
    }

    [Fact]
    public async Task Email_still_pending_after_its_day_is_skipped()
    {
        await using var fixture = await Fixture.CreateAsync();

        // The application was down from before 09:00 until the next morning.
        Assert.Equal(0, await fixture.DispatchAtAsync(new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc)));

        Assert.Empty(fixture.Sender.Sent);
        Assert.All(await fixture.EmailsAsync(), email =>
        {
            Assert.Equal(EmailDeliveryStatus.Skipped, email.Status);
            Assert.Equal(EmailDispatcher.ExpiredReason, email.LastError);
            Assert.Equal(0, email.Attempts);
        });
    }

    [Fact]
    public async Task Task_completed_before_the_send_time_is_skipped()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Harness.Db.Tasks
            .Where(task => task.Id == fixture.Task.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(task => task.Status, TaskStatus.Completed));

        await fixture.DispatchAtAsync(SendTime);

        Assert.Empty(fixture.Sender.Sent);
        Assert.All(await fixture.EmailsAsync(), email =>
        {
            Assert.Equal(EmailDeliveryStatus.Skipped, email.Status);
            Assert.Equal(EmailDispatcher.TaskChangedReason, email.LastError);
        });
    }

    [Fact]
    public async Task Rescheduled_task_is_skipped()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Harness.Db.Tasks
            .Where(task => task.Id == fixture.Task.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(task => task.Deadline, Deadline.AddDays(5)));

        await fixture.DispatchAtAsync(SendTime);

        Assert.Empty(fixture.Sender.Sent);
        Assert.All(await fixture.EmailsAsync(), email => Assert.Equal(EmailDispatcher.TaskChangedReason, email.LastError));
    }

    [Fact]
    public async Task Disabling_email_skips_queued_emails()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Set(SettingKeys.NotificationEmailEnabled, "false");

        await fixture.DispatchAtAsync(SendTime);

        Assert.Empty(fixture.Sender.Sent);
        Assert.All(await fixture.EmailsAsync(), email =>
        {
            Assert.Equal(EmailDeliveryStatus.Skipped, email.Status);
            Assert.Equal(EmailDispatcher.EmailDisabledReason, email.LastError);
        });
    }

    [Fact]
    public async Task Email_claimed_by_another_worker_is_not_sent_twice()
    {
        await using var fixture = await Fixture.CreateAsync();
        // While the first worker sends its first email, a second worker processes the same batch.
        fixture.Sender.BeforeSend = () => fixture.Dispatcher().DispatchDueAsync();

        await fixture.DispatchAtAsync(SendTime);

        var addresses = fixture.Sender.Sent.Select(message => message.ToAddress).ToList();
        Assert.Equal(2, addresses.Count);
        Assert.Equal(addresses.Count, addresses.Distinct().Count());
        Assert.All(await fixture.EmailsAsync(), email =>
        {
            Assert.Equal(EmailDeliveryStatus.Sent, email.Status);
            Assert.Equal(1, email.Attempts);
        });
    }
}
