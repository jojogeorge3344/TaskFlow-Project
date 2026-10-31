using TaskFlow.Api.Services.Communication.Notification.Reminders;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Services.Settings.ReminderSettings;
using TaskFlow.Api.Config;
using System.Data.Common;
using TaskFlow.Api.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using Task = System.Threading.Tasks.Task;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Services.Settings.ApplicationSettings.Clock;

namespace TaskFlow.UnitTests.Communication.Notification.Reminders;

/// <summary>Runs the reminder engine against a real SQLite file with separate DbContexts, the way repeated
/// scheduler passes and restarts see it.</summary>
public sealed class ReminderIdempotencyTests : IAsyncLifetime
{
    private static readonly DateTime Deadline = new(2026, 10, 15, 17, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"taskflow-reminders-{Guid.NewGuid():N}.db");
    private readonly FixedTimeProvider _clock = new(new DateTime(2026, 10, 13, 9, 0, 0));
    private int _taskId;
    private int _recipientCount;

    public async Task InitializeAsync()
    {
        await using var db = NewContext();
        await db.Database.EnsureCreatedAsync();
        await TestAccessControl.SeedAsync(db);

        var creator = AddUser(db, "idem-creator", SystemRoles.Manager);
        var assignee = AddUser(db, "idem-assignee", SystemRoles.Employee);
        var extra = AddUser(db, "idem-extra", SystemRoles.Employee);
        await db.SaveChangesAsync();

        var task = new TaskEntity
        {
            TaskCode = "TSK-IDEM",
            Title = "Prepare Monthly Report",
            AssignedToUserId = assignee.Id,
            CreatedByUserId = creator.Id,
            Priority = TaskPriority.High,
            StartDate = Deadline.AddDays(-7),
            Deadline = Deadline,
            EstimatedHours = 4,
            Status = TaskStatus.InProgress,
            CreatedAt = Deadline.AddDays(-7),
            UpdatedAt = Deadline.AddDays(-7)
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        db.TaskRecipients.Add(new TaskRecipient { TaskId = task.Id, UserId = assignee.Id });
        db.TaskRecipients.Add(new TaskRecipient { TaskId = task.Id, UserId = extra.Id });
        await db.SaveChangesAsync();

        _taskId = task.Id;
        _recipientCount = 3;
    }

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Repeated_passes_at_the_same_instant_deliver_once()
    {
        for (var pass = 0; pass < 5; pass++)
        {
            await RunPassAsync();
        }

        Assert.Equal(_recipientCount, await CountAsync(db => db.ReminderHistory));
        Assert.Equal(_recipientCount, await CountAsync(db => db.Notifications));
        Assert.Equal(_recipientCount, await CountAsync(db => db.MailMessages));
        Assert.Equal(_recipientCount, await CountAsync(db => db.MailboxItems));
    }

    [Fact]
    public async Task Separate_scopes_including_concurrent_ones_deliver_once()
    {
        await Task.WhenAll(RunPassAsync(), RunPassAsync());
        await RunPassAsync();

        Assert.Equal(_recipientCount, await CountAsync(db => db.ReminderHistory));
        Assert.Equal(_recipientCount, await CountAsync(db => db.Notifications));
        Assert.Equal(_recipientCount, await CountAsync(db => db.MailMessages));
        Assert.Equal(_recipientCount, await CountAsync(db => db.MailboxItems));
    }

    [Fact]
    public async Task Next_day_brings_the_next_reminder_once()
    {
        await RunPassAsync();
        _clock.Set(new DateTime(2026, 10, 14, 9, 0, 0));
        await RunPassAsync();
        await RunPassAsync();

        await using var db = NewContext();
        var types = await db.ReminderHistory.Select(row => row.ReminderType).Distinct().ToListAsync();
        Assert.Equal(new[] { ReminderType.FirstReminder, ReminderType.SecondReminder }.Order(), types.Order());
        Assert.Equal(_recipientCount * 2, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Completing_the_task_stops_further_reminders()
    {
        await RunPassAsync();
        await using (var db = NewContext())
        {
            var task = await db.Tasks.SingleAsync(t => t.Id == _taskId);
            task.Status = TaskStatus.Completed;
            await db.SaveChangesAsync();
        }

        foreach (var day in new[] { 14, 15, 16, 20 })
        {
            _clock.Set(new DateTime(2026, 10, day, 9, 0, 0));
            await RunPassAsync();
        }

        Assert.Equal(_recipientCount, await CountAsync(db => db.Notifications));
    }

    [Fact]
    public async Task Deadline_change_moves_reminders_to_the_new_date()
    {
        await using (var db = NewContext())
        {
            var task = await db.Tasks.SingleAsync(t => t.Id == _taskId);
            task.Deadline = new DateTime(2026, 10, 20, 17, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }

        foreach (var day in new[] { 13, 14, 15 })
        {
            _clock.Set(new DateTime(2026, 10, day, 9, 0, 0));
            await RunPassAsync();
        }

        Assert.Equal(0, await CountAsync(db => db.Notifications));

        _clock.Set(new DateTime(2026, 10, 18, 9, 0, 0));
        await RunPassAsync();

        await using var check = NewContext();
        var history = await check.ReminderHistory.ToListAsync();
        Assert.Equal(_recipientCount, history.Count);
        Assert.All(history, row => Assert.Equal(ReminderType.FirstReminder, row.ReminderType));
        Assert.All(history, row => Assert.Equal(new DateTime(2026, 10, 18), row.ScheduledFor));
    }

    [Fact]
    public async Task Later_passes_the_same_day_skip_delivered_reminders_without_writing()
    {
        await RunPassAsync();
        var interceptor = new HistoryInsertInterceptor();

        await RunPassAsync(interceptor);
        await RunPassAsync(interceptor);

        Assert.Equal(0, interceptor.HistoryInserts);
        Assert.Equal(_recipientCount, await CountAsync(db => db.Notifications));
    }

    [Fact]
    public async Task Task_completed_during_a_pass_gets_no_reminder()
    {
        var interceptor = new HistoryInsertInterceptor();
        var edit = new ConcurrentEditInterceptor(() => ExecuteAsync($"UPDATE \"Tasks\" SET \"Status\" = {(int)TaskStatus.Completed}"));

        await RunPassAsync(interceptor, edit);

        Assert.True(interceptor.HistoryInserts > 0);
        Assert.Equal(0, await CountAsync(db => db.ReminderHistory));
        Assert.Equal(0, await CountAsync(db => db.Notifications));
    }

    [Fact]
    public async Task Task_rescheduled_during_a_pass_gets_no_reminder_for_the_old_deadline()
    {
        var edit = new ConcurrentEditInterceptor(() => ExecuteAsync("UPDATE \"Tasks\" SET \"Deadline\" = '2026-11-30 17:00:00'"));

        await RunPassAsync(new HistoryInsertInterceptor(), edit);

        Assert.Equal(0, await CountAsync(db => db.Notifications));
    }

    /// <summary>Runs SQL on its own connection, like another request committing a change.</summary>
    private async Task ExecuteAsync(string sql)
    {
        await using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task RunPassAsync(params IInterceptor[] interceptors)
    {
        await using var db = NewContext(interceptors);
        var options = Options.Create(new ApplicationOptions { Timezone = "UTC" });
        var service = TestServices.Reminders(
            db,
            new AppClock(new ApplicationSettingsRepository(db), _clock, options, NullLogger<AppClock>.Instance),
            new ReminderSettingsService(new ApplicationSettingsRepository(db), new UnitOfWork(db), _clock, options),
            Options.Create(new TaskFlow.Api.Config.MailOptions { AppBaseUrl = "http://localhost:4200" }),
            NullLogger<ReminderService>.Instance);

        await service.GenerateDueAsync();
    }

    private async Task<int> CountAsync<T>(Func<AppDbContext, IQueryable<T>> query)
    {
        await using var db = NewContext();
        return await query(db).CountAsync();
    }

    private AppDbContext NewContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath};Default Timeout=30");
        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        return new AppDbContext(options.Options);
    }

    /// <summary>Runs an edit on another connection just before the pass opens its first delivery transaction,
    /// i.e. after the pass has already read its candidate tasks.</summary>
    private sealed class ConcurrentEditInterceptor(Func<Task> edit) : DbTransactionInterceptor
    {
        private Func<Task>? _edit = edit;

        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            if (_edit is { } pending)
            {
                _edit = null;
                await pending();
            }

            return result;
        }
    }

    /// <summary>Counts reminder-history insert attempts.</summary>
    private sealed class HistoryInsertInterceptor : DbCommandInterceptor
    {
        public int HistoryInserts { get; private set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO \"ReminderHistory\"", StringComparison.Ordinal))
            {
                HistoryInserts++;
            }

            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static User AddUser(AppDbContext db, string username, string role)
    {
        var user = new User
        {
            FullName = $"{username} full name",
            Username = username,
            Email = $"{username}@taskflow.com",
            PasswordHash = "hash",
            RoleId = TestAccessControl.IdFor(role),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        return user;
    }
}
