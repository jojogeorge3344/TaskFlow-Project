using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Reminders;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Rules;
using TaskFlow.Infrastructure.Persistence;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Turns the reminders <see cref="ReminderCalculator"/> says are due today into reminder history
/// rows and in-app notifications. Duplicate prevention lives in the database: the history insert is an
/// insert-or-ignore on the unique (TaskId, RecipientUserId, ReminderType, ScheduledFor) index, and the
/// notification is written only when that insert actually added a row.</summary>
public class ReminderService : IReminderService
{
    // Widest pre-deadline interval (30 days) plus timezone slack.
    private static readonly TimeSpan UpcomingWindow = TimeSpan.FromDays(32);

    // How far back overdue tasks keep receiving reminders.
    private static readonly TimeSpan OverdueWindow = TimeSpan.FromDays(400);

    private readonly AppDbContext _dbContext;
    private readonly IAppClock _clock;
    private readonly IReminderSettingsService _settingsService;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(
        AppDbContext dbContext,
        IAppClock clock,
        IReminderSettingsService settingsService,
        ILogger<ReminderService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<int> GenerateDueAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            return 0;
        }

        var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
        var nowUtc = _clock.UtcNow;
        var from = nowUtc - OverdueWindow;
        var to = nowUtc + UpcomingWindow;

        var candidates = await _dbContext.Tasks
            .AsNoTracking()
            .Where(task => !task.IsDeleted
                && task.Status != TaskStatus.Completed
                && task.Status != TaskStatus.Cancelled
                && task.Deadline >= from
                && task.Deadline <= to)
            .Select(task => new
            {
                task.Id,
                task.Title,
                task.CreatedByUserId,
                Snapshot = new ReminderTaskSnapshot(
                    task.Status,
                    task.Deadline,
                    task.FirstReminderDays,
                    task.SecondReminderDays,
                    task.OverdueReminderFrequency)
            })
            .ToListAsync(cancellationToken);

        var dueTasks = candidates
            .Select(task => new
            {
                task.Id,
                task.Title,
                task.CreatedByUserId,
                task.Snapshot,
                Reminders = ReminderCalculator.DueReminders(task.Snapshot, nowUtc, timeZone, settings.DeadlineDayEnabled)
            })
            .Where(task => task.Reminders.Count > 0)
            .ToList();

        if (dueTasks.Count == 0)
        {
            return 0;
        }

        var recipientsByTask = await LoadRecipientsAsync(
            dueTasks.ToDictionary(task => task.Id, task => task.CreatedByUserId),
            cancellationToken);

        var written = 0;
        foreach (var task in dueTasks)
        {
            foreach (var reminder in task.Reminders)
            {
                var message = BuildMessage(task.Title, task.Snapshot.DeadlineUtc, reminder, timeZone);
                foreach (var recipientId in recipientsByTask[task.Id])
                {
                    if (await TryDeliverAsync(task.Id, recipientId, reminder, message, nowUtc, cancellationToken))
                    {
                        written++;
                    }
                }
            }
        }

        if (written > 0)
        {
            _logger.LogInformation("Generated {Count} deadline reminder notification(s).", written);
        }

        return written;
    }

    /// <summary>Reminder recipients per task: its notification recipients (the assignee is always one) plus
    /// the creator, de-duplicated and limited to active, non-deleted users.</summary>
    private async Task<Dictionary<int, IReadOnlyList<int>>> LoadRecipientsAsync(
        IReadOnlyDictionary<int, int> creatorByTask,
        CancellationToken cancellationToken)
    {
        var taskIds = creatorByTask.Keys.ToList();
        var recipientRows = await _dbContext.TaskRecipients
            .AsNoTracking()
            .Where(recipient => taskIds.Contains(recipient.TaskId))
            .Select(recipient => new { recipient.TaskId, recipient.UserId })
            .ToListAsync(cancellationToken);

        var candidatesByTask = taskIds.ToDictionary(
            taskId => taskId,
            taskId => recipientRows
                .Where(row => row.TaskId == taskId)
                .Select(row => row.UserId)
                .Append(creatorByTask[taskId])
                .Distinct()
                .ToList());

        var allUserIds = candidatesByTask.Values.SelectMany(ids => ids).Distinct().ToList();

        // Inactive or deleted users receive no reminders.
        var activeUserIds = (await _dbContext.Users
                .AsNoTracking()
                .Where(user => allUserIds.Contains(user.Id) && user.IsActive && !user.IsDeleted)
                .Select(user => user.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return candidatesByTask.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<int>)pair.Value.Where(activeUserIds.Contains).ToList());
    }

    /// <summary>Records the reminder and writes its notification in one transaction. Returns false when the
    /// same reminder was already delivered, in which case nothing is written.</summary>
    private async Task<bool> TryDeliverAsync(
        int taskId,
        int recipientId,
        DueReminder reminder,
        string message,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var reminderType = reminder.Type.ToString();
        var sent = ReminderStatus.Sent.ToString();
        var inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "ReminderHistory" ("TaskId", "RecipientUserId", "ReminderType", "ScheduledFor", "Status", "ProcessedAt", "DeliveryAttempts")
            VALUES ({taskId}, {recipientId}, {reminderType}, {reminder.ScheduledForUtc}, {sent}, {nowUtc}, 0)
            ON CONFLICT ("TaskId", "RecipientUserId", "ReminderType", "ScheduledFor") DO NOTHING
            """,
            cancellationToken);

        if (inserted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var notification = new Notification
        {
            UserId = recipientId,
            TaskId = taskId,
            Type = reminder.Type == ReminderType.Overdue ? NotificationType.Overdue : NotificationType.DeadlineReminder,
            Message = message,
            IsRead = false,
            CreatedAt = nowUtc
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _dbContext.Entry(notification).State = EntityState.Detached;

        return true;
    }

    private static string BuildMessage(string title, DateTime deadlineUtc, DueReminder reminder, TimeZoneInfo timeZone)
    {
        var deadlineLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(deadlineUtc, DateTimeKind.Utc), timeZone);
        var culture = CultureInfo.InvariantCulture;

        switch (reminder.Type)
        {
            case ReminderType.DueToday:
                return $"Reminder: \"{title}\" is due today at {deadlineLocal.ToString("HH:mm", culture)}.";
            case ReminderType.Overdue:
                return $"Overdue: \"{title}\" was due on {deadlineLocal.ToString("dd MMM yyyy, HH:mm", culture)}.";
            default:
                var days = DateOnly.FromDateTime(deadlineLocal).DayNumber - reminder.Day.DayNumber;
                var unit = days == 1 ? "day" : "days";
                return $"Reminder: \"{title}\" is due in {days} {unit} ({deadlineLocal.ToString("dd MMM yyyy, HH:mm", culture)}).";
        }
    }
}
