using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Email;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Email;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Settings;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Sends queued emails after each reminder pass. Each email is claimed with a conditional update
/// before it is sent, so overlapping passes never send it twice; the claim also leases the row, so an email
/// whose sender crashed mid-send is retried once the lease runs out. Sending happens outside any database
/// transaction, so SMTP problems can never touch in-app notifications.</summary>
public class EmailDispatcher : IEmailDispatcher
{
    public const int BatchSize = 50;
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(10);

    public const string ExpiredReason = "Reminder day ended before the email could be sent.";
    public const string TaskChangedReason = "Task changed before sending.";
    public const string EmailDisabledReason = "Email notifications were disabled.";
    public const string ConfigurationPrefix = "Email configuration is incomplete: ";

    // Wait after the 1st and 2nd failed attempts; the last value is reused if more attempts are configured.
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    private readonly AppDbContext _dbContext;
    private readonly IEmailSender _sender;
    private readonly IEmailSettingsService _settingsService;
    private readonly TimeProvider _timeProvider;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailDispatcher> _logger;

    public EmailDispatcher(
        AppDbContext dbContext,
        IEmailSender sender,
        IEmailSettingsService settingsService,
        TimeProvider timeProvider,
        IOptions<EmailOptions> options,
        ILogger<EmailDispatcher> logger)
    {
        _dbContext = dbContext;
        _sender = sender;
        _settingsService = settingsService;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var due = await _dbContext.EmailDeliveryLogs
            .AsNoTracking()
            .Where(email => email.Status == EmailDeliveryStatus.Pending && email.NextAttemptAt <= now)
            .OrderBy(email => email.NextAttemptAt)
            .ThenBy(email => email.Id)
            .Take(BatchSize)
            .Select(email => new
            {
                email.Id,
                email.Kind,
                email.ReminderHistoryId,
                email.ToAddress,
                email.Subject,
                email.Body,
                email.Attempts,
                email.ExpiresAt,
                email.TaskDeadline,
                TaskMissing = email.Task == null,
                TaskDeleted = email.Task != null && email.Task.IsDeleted,
                TaskStatus = email.Task != null ? email.Task.Status : (TaskStatus?)null,
                TaskDeadlineNow = email.Task != null ? email.Task.Deadline : (DateTime?)null
            })
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return 0;
        }

        var schedule = await _settingsService.GetScheduleAsync(cancellationToken);
        SmtpSettings? smtp = null;
        var maxAttempts = Math.Max(1, _options.MaxAttempts);
        var sent = 0;

        foreach (var email in due)
        {
            if (email.Kind == EmailKind.Reminder)
            {
                string? skipReason = null;
                if (email.ExpiresAt is { } expiresAt && now >= expiresAt)
                {
                    skipReason = ExpiredReason;
                }
                else if (email.TaskMissing
                    || email.TaskDeleted
                    || email.TaskStatus is TaskStatus.Completed or TaskStatus.Cancelled
                    || (email.TaskDeadline is { } queuedDeadline && email.TaskDeadlineNow != queuedDeadline))
                {
                    skipReason = TaskChangedReason;
                }
                else if (!schedule.Enabled)
                {
                    skipReason = EmailDisabledReason;
                }

                if (skipReason is not null)
                {
                    await _dbContext.EmailDeliveryLogs
                        .Where(row => row.Id == email.Id && row.Status == EmailDeliveryStatus.Pending && row.Attempts == email.Attempts)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(row => row.Status, EmailDeliveryStatus.Skipped)
                                .SetProperty(row => row.LastError, skipReason)
                                .SetProperty(row => row.NextAttemptAt, (DateTime?)null),
                            cancellationToken);
                    continue;
                }
            }

            // Claim: only the pass whose update matches the attempt count it read may send.
            var attempt = email.Attempts + 1;
            var claimed = await _dbContext.EmailDeliveryLogs
                .Where(row => row.Id == email.Id && row.Status == EmailDeliveryStatus.Pending && row.Attempts == email.Attempts)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(row => row.Attempts, attempt)
                        .SetProperty(row => row.LastAttemptAt, now)
                        .SetProperty(row => row.NextAttemptAt, now + ClaimLease),
                    cancellationToken);
            if (claimed == 0)
            {
                continue;
            }

            if (email.ReminderHistoryId is { } historyId)
            {
                await _dbContext.ReminderHistory
                    .Where(row => row.Id == historyId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(row => row.DeliveryAttempts, row => row.DeliveryAttempts + 1),
                        cancellationToken);
            }

            smtp ??= await _settingsService.GetSmtpSettingsAsync(cancellationToken);
            var problems = EmailConfigurationCheck.Problems(smtp);
            var result = problems.Count > 0
                ? EmailSendResult.Failed(ConfigurationPrefix + string.Join(" ", problems))
                : await _sender.SendAsync(new EmailMessage(email.ToAddress, email.Subject, email.Body), smtp, cancellationToken);

            if (result.Success)
            {
                sent++;
                await _dbContext.EmailDeliveryLogs
                    .Where(row => row.Id == email.Id)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(row => row.Status, EmailDeliveryStatus.Sent)
                            .SetProperty(row => row.SentAt, UtcNow)
                            .SetProperty(row => row.LastError, (string?)null)
                            .SetProperty(row => row.NextAttemptAt, (DateTime?)null),
                        cancellationToken);
                continue;
            }

            var error = Truncate(result.Error ?? "Email delivery failed.");
            var finalAttempt = attempt >= maxAttempts;
            var retryAt = finalAttempt ? (DateTime?)null : UtcNow + RetryDelays[Math.Min(attempt, RetryDelays.Length) - 1];
            await _dbContext.EmailDeliveryLogs
                .Where(row => row.Id == email.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(row => row.Status, finalAttempt ? EmailDeliveryStatus.Failed : EmailDeliveryStatus.Pending)
                        .SetProperty(row => row.LastError, error)
                        .SetProperty(row => row.NextAttemptAt, retryAt),
                    cancellationToken);

            _logger.LogWarning(
                "Email {DeliveryId} attempt {Attempt} of {MaxAttempts} failed: {Error}",
                email.Id,
                attempt,
                maxAttempts,
                error);
        }

        if (sent > 0)
        {
            _logger.LogInformation("Sent {Count} email(s).", sent);
        }

        return sent;
    }

    private static string Truncate(string value) =>
        value.Length <= MailKitEmailSender.MaxErrorLength ? value : value[..MailKitEmailSender.MaxErrorLength];
}
