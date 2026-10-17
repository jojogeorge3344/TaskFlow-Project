using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Email;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Sends test emails synchronously and reads the delivery log (Screen 14).</summary>
public class EmailDeliveryService : IEmailDeliveryService
{
    public const string TestSubject = "TaskFlow test email";

    private readonly AppDbContext _dbContext;
    private readonly IEmailSender _sender;
    private readonly IEmailSettingsService _settingsService;
    private readonly TimeProvider _timeProvider;

    public EmailDeliveryService(
        AppDbContext dbContext,
        IEmailSender sender,
        IEmailSettingsService settingsService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _sender = sender;
        _settingsService = settingsService;
        _timeProvider = timeProvider;
    }

    public async Task<TestEmailResultDto?> SendTestAsync(
        int userId,
        string? toAddress,
        CancellationToken cancellationToken = default)
    {
        var address = toAddress?.Trim();
        if (string.IsNullOrEmpty(address))
        {
            address = (await _dbContext.Users
                .Where(user => user.Id == userId)
                .Select(user => user.Email)
                .FirstOrDefaultAsync(cancellationToken))?.Trim();
        }

        if (string.IsNullOrEmpty(address))
        {
            return null;
        }

        var smtp = await _settingsService.GetSmtpSettingsAsync(cancellationToken);
        var body = "This is a test email from TaskFlow. If you received it, email delivery for deadline reminders is configured correctly.";
        var attemptedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var problems = EmailConfigurationCheck.Problems(smtp);
        var result = problems.Count > 0
            ? EmailSendResult.Failed(EmailDispatcher.ConfigurationPrefix + string.Join(" ", problems))
            : await _sender.SendAsync(new EmailMessage(address, TestSubject, body), smtp, cancellationToken);

        var log = new EmailDeliveryLog
        {
            Kind = EmailKind.Test,
            ToAddress = address,
            Subject = TestSubject,
            Body = body,
            Status = result.Success ? EmailDeliveryStatus.Sent : EmailDeliveryStatus.Failed,
            Attempts = 1,
            LastError = result.Success ? null : result.Error,
            CreatedAt = attemptedAt,
            LastAttemptAt = attemptedAt,
            SentAt = result.Success ? _timeProvider.GetUtcNow().UtcDateTime : null,
            CreatedByUserId = userId
        };
        _dbContext.EmailDeliveryLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TestEmailResultDto(
            result.Success,
            result.Success ? $"Test email sent to {address}." : result.Error ?? "Email delivery failed.",
            log.Id);
    }

    public async Task<IReadOnlyList<EmailDeliveryDto>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        var count = Math.Clamp(take, 1, IEmailDeliveryService.MaxRecentCount);

        return await _dbContext.EmailDeliveryLogs
            .AsNoTracking()
            .OrderByDescending(email => email.CreatedAt)
            .ThenByDescending(email => email.Id)
            .Take(count)
            .Select(email => new EmailDeliveryDto(
                email.Id,
                email.Kind,
                email.ToAddress,
                email.Subject,
                email.TaskId,
                email.Task != null ? email.Task.Title : null,
                email.Status,
                email.Attempts,
                email.LastError,
                email.CreatedAt,
                email.LastAttemptAt,
                email.NextAttemptAt,
                email.SentAt))
            .ToListAsync(cancellationToken);
    }
}
