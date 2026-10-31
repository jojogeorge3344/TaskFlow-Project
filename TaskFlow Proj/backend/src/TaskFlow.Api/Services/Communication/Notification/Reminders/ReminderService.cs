using NotificationEntity = TaskFlow.Api.Models.Entities.Notification;
using TaskFlow.Api.Config;
using TaskFlow.Api.Services.Settings.ReminderSettings;
using TaskFlow.Api.Services.Settings.ApplicationSettings;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Repositories.Communication.Mail;
using TaskFlow.Api.Repositories.Communication.Notification;
using TaskFlow.Api.Repositories.Communication.Notification.Reminders;
using TaskFlow.Api.Repositories.Management.EmployeeManagement;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Services.Communication.Notification.Reminders;

/// <summary>Turns the reminders <see cref="ReminderCalculator"/> says are due today into reminder history
/// rows and in-app notifications. Duplicate prevention lives in the database: the history insert is an
/// insert-or-ignore on the unique (TaskId, RecipientUserId, ReminderType, ScheduledFor) index, and the
/// notification is written only when that insert actually added a row.</summary>
public class ReminderService : IReminderService
{
    // Widest pre-deadline interval (30 days) plus timezone slack. There is no lower bound: overdue tasks
    // keep receiving reminders for as long as they stay open.
    private static readonly TimeSpan UpcomingWindow = TimeSpan.FromDays(32);

    private readonly IReminderRepository _reminders;
    private readonly INotificationRepository _notifications;
    private readonly IMailRepository _mail;
    private readonly IEmployeeRepository _employees;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppClock _clock;
    private readonly IReminderSettingsService _settingsService;
    private readonly MailOptions _mailOptions;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(
        IReminderRepository reminders,
        INotificationRepository notifications,
        IMailRepository mail,
        IEmployeeRepository employees,
        IUnitOfWork unitOfWork,
        IAppClock clock,
        IReminderSettingsService settingsService,
        IOptions<MailOptions> mailOptions,
        ILogger<ReminderService> logger)
    {
        _reminders = reminders;
        _notifications = notifications;
        _mail = mail;
        _employees = employees;
        _unitOfWork = unitOfWork;
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

        var candidates = await _reminders.GetOpenTasksDueByAsync(to, cancellationToken);

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
        var rows = await _reminders.GetDeliveredAsync(taskIds, earliestSlot, cancellationToken);

        // DateTime equality ignores Kind, so slots read back from the database match the computed UTC slots.
        return rows.Select(row => (row.TaskId, row.RecipientUserId, row.Type, row.ScheduledFor)).ToHashSet();
    }

    /// <summary>Reminder recipients per task: its notification recipients (the assignee is always one) plus
    /// the creator, de-duplicated and limited to active, non-deleted users.</summary>
    private async Task<Dictionary<int, IReadOnlyList<int>>> LoadRecipientsAsync(
        IReadOnlyDictionary<int, int> creatorByTask,
        CancellationToken cancellationToken)
    {
        var taskIds = creatorByTask.Keys.ToList();
        var recipientRows = await _reminders.GetTaskRecipientsAsync(taskIds, cancellationToken);

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
        var activeUserIds = (await _employees.GetActiveByIdsAsync(allUserIds, cancellationToken)).Select(user => user.Id).ToHashSet();

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
        var delivered = await _unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                if (!await _reminders.TryRecordAsync(taskId, recipientId, reminder.Type, reminder.ScheduledForUtc, deadlineUtc, nowUtc, token))
                {
                    // Nothing was written, so the empty transaction commits as a no-op.
                    return false;
                }

                _notifications.Add(new NotificationEntity
                {
                    UserId = recipientId,
                    TaskId = taskId,
                    Type = reminder.Type == ReminderType.Overdue ? NotificationType.Overdue : NotificationType.DeadlineReminder,
                    Message = message,
                    IsRead = false,
                    CreatedAt = nowUtc
                });

                if (mail is { } reminderMail)
                {
                    var historyId = await _reminders.GetIdAsync(taskId, recipientId, reminder.Type, reminder.ScheduledForUtc, token);

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
                    _mail.Add(mailMessage);
                    _mail.AddRecipient(new MailRecipient { Message = mailMessage, UserId = recipientId, Kind = MailRecipientKind.To });
                    _mail.AddMailboxItem(new MailboxItem { Message = mailMessage, OwnerUserId = recipientId, Folder = MailFolder.Inbox, SortAt = nowUtc });
                }

                await _unitOfWork.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);

        if (delivered)
        {
            _unitOfWork.DetachAll();
        }

        return delivered;
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
