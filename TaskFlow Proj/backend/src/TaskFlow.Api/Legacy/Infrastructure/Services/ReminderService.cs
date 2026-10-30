using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Mail;
using TaskFlow.Application.Reminders;
using TaskFlow.Application.Settings;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Infrastructure.Mail;
using TaskFlow.Api.Persistence;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Turns the reminders <see cref="ReminderCalculator"/> says are due today into reminder history
/// rows and in-app notifications. Duplicate prevention lives in the database: the history insert is an
/// insert-or-ignore on the unique (TaskId, RecipientUserId, ReminderType, ScheduledFor) index, and the
/// notification is written only when that insert actually added a row.</summary>
public class ReminderService : IReminderService
{
    // Widest pre-deadline interval (30 days) plus timezone slack. There is no lower bound: overdue tasks
    // keep receiving reminders for as long as they stay open.
    private static readonly TimeSpan UpcomingWindow = TimeSpan.FromDays(32);

    private readonly AppDbContext _dbContext;
    private readonly IAppClock _clock;
    private readonly IReminderSettingsService _settingsService;
    private readonly MailOptions _mailOptions;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(
        AppDbContext dbContext,
        IAppClock clock,
        IReminderSettingsService settingsService,
        IOptions<MailOptions> mailOptions,
        ILogger<ReminderService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _settingsService = settingsService;
        _mailOptions = mailOptions.Value;
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
        var to = nowUtc + UpcomingWindow;

        var candidates = await _dbContext.Tasks
            .AsNoTracking()
            .Where(task => !task.IsDeleted
                && task.Status != TaskStatus.Completed
                && task.Status != TaskStatus.Cancelled
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

        var delivered = await LoadDeliveredAsync(dueTasks.Select(task => task.Id).ToList(), dueTasks.SelectMany(task => task.Reminders), cancellationToken);

        var written = 0;
        foreach (var task in dueTasks)
        {
            foreach (var reminder in task.Reminders)
            {
                var message = BuildMessage(task.Title, task.Snapshot.DeadlineUtc, reminder, timeZone);
                (string Subject, string Body)? mail = settings.MailEnabled ? BuildMail(task.Id, task.Title, reminder, message) : null;
                foreach (var recipientId in recipientsByTask[task.Id])
                {
                    // Already delivered earlier today: skip without opening a write transaction.
                    if (delivered.Contains((task.Id, recipientId, reminder.Type, reminder.ScheduledForUtc)))
                    {
                        continue;
                    }

                    if (await TryDeliverAsync(task.Id, task.Snapshot.DeadlineUtc, recipientId, reminder, message, mail, nowUtc, cancellationToken))
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

    /// <summary>Reminders already in the history for today's slots, so a pass can skip them without taking the
    /// SQLite write lock. The unique index still guards against anything this read misses.</summary>
    private async Task<HashSet<(int TaskId, int RecipientId, ReminderType Type, DateTime ScheduledFor)>> LoadDeliveredAsync(
        IReadOnlyCollection<int> taskIds,
        IEnumerable<DueReminder> reminders,
        CancellationToken cancellationToken)
    {
        var earliestSlot = reminders.Min(reminder => reminder.ScheduledForUtc);
        var rows = await _dbContext.ReminderHistory
            .AsNoTracking()
            .Where(row => taskIds.Contains(row.TaskId) && row.ScheduledFor >= earliestSlot)
            .Select(row => new { row.TaskId, row.RecipientUserId, row.ReminderType, row.ScheduledFor })
            .ToListAsync(cancellationToken);

        // DateTime equality ignores Kind, so slots read back from the database match the computed UTC slots.
        return rows.Select(row => (row.TaskId, row.RecipientUserId, row.ReminderType, row.ScheduledFor)).ToHashSet();
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

    /// <summary>Records the reminder and writes its notification, and its Inbox mail when one is given, in one
    /// transaction. Returns false when the same reminder was already delivered, or when the task was completed,
    /// cancelled, deleted or rescheduled since this pass read it; in either case nothing is written.</summary>
    private async Task<bool> TryDeliverAsync(
        int taskId,
        DateTime deadlineUtc,
        int recipientId,
        DueReminder reminder,
        string message,
        (string Subject, string Body)? mail,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var reminderType = reminder.Type.ToString();
        var sent = ReminderStatus.Sent.ToString();
        // Task.Status is stored as its integer value (no string conversion on Tasks).
        var completed = (int)TaskStatus.Completed;
        var cancelled = (int)TaskStatus.Cancelled;
        var inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "ReminderHistory" ("TaskId", "RecipientUserId", "ReminderType", "ScheduledFor", "Status", "ProcessedAt", "DeliveryAttempts")
            SELECT {taskId}, {recipientId}, {reminderType}, {reminder.ScheduledForUtc}, {sent}, {nowUtc}, 0
            WHERE EXISTS (
                SELECT 1 FROM "Tasks"
                WHERE "Id" = {taskId}
                  AND "IsDeleted" = 0
                  AND "Status" NOT IN ({completed}, {cancelled})
                  AND "Deadline" = {deadlineUtc})
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

        if (mail is { } reminderMail)
        {
            var historyId = await _dbContext.ReminderHistory
                .Where(row => row.TaskId == taskId
                    && row.RecipientUserId == recipientId
                    && row.ReminderType == reminder.Type
                    && row.ScheduledFor == reminder.ScheduledForUtc)
                .Select(row => row.Id)
                .SingleAsync(cancellationToken);

            // From the TaskFlow system sender; the unique ReminderHistoryId backs up the history dedup.
            var mailMessage = new MailMessage
            {
                ConversationId = Guid.NewGuid(),
                IsSystem = true,
                Subject = reminderMail.Subject,
                Body = reminderMail.Body,
                TaskId = taskId,
                ReminderHistoryId = historyId,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc,
                SentAt = nowUtc
            };
            _dbContext.MailMessages.Add(mailMessage);
            _dbContext.MailRecipients.Add(new MailRecipient { Message = mailMessage, UserId = recipientId, Kind = MailRecipientKind.To });
            _dbContext.MailboxItems.Add(new MailboxItem { Message = mailMessage, OwnerUserId = recipientId, Folder = MailFolder.Inbox, SortAt = nowUtc });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();

        return true;
    }

    /// <summary>The Inbox mail for a reminder: a subject naming the kind of reminder, the notification text and,
    /// when the app address is configured, a link to the task.</summary>
    private (string Subject, string Body) BuildMail(int taskId, string title, DueReminder reminder, string message)
    {
        var prefix = reminder.Type switch
        {
            ReminderType.Overdue => "Overdue task: ",
            ReminderType.DueToday => "Task due today: ",
            _ => "Task reminder: "
        };
        var subject = prefix + title;
        if (subject.Length > MailRules.MaxSubjectLength)
        {
            subject = subject[..MailRules.MaxSubjectLength];
        }

        var body = new System.Text.StringBuilder().AppendLine(message);
        if (!string.IsNullOrWhiteSpace(_mailOptions.AppBaseUrl))
        {
            body.AppendLine().Append("Open the task: ").Append(_mailOptions.AppBaseUrl.TrimEnd('/')).Append("/tasks/").Append(taskId).AppendLine();
        }

        body.AppendLine().Append("This is an automatic reminder from TaskFlow.");
        return (subject, body.ToString());
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
