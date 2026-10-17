using TaskFlow.Application.Settings;

namespace TaskFlow.Application.Email;

/// <summary>A plain-text email to one recipient.</summary>
public record EmailMessage(string ToAddress, string Subject, string Body);

/// <summary>The outcome of one send. <see cref="Error"/> is a fixed, readable message that never contains
/// credentials, safe to store in the delivery log and show to Administrators.</summary>
public record EmailSendResult(bool Success, string? Error)
{
    public static EmailSendResult Sent { get; } = new(true, null);

    public static EmailSendResult Failed(string error) => new(false, error);
}

/// <summary>Sends one email over SMTP. SMTP problems are returned as a failed result, never thrown.</summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, SmtpSettings settings, CancellationToken cancellationToken = default);
}
