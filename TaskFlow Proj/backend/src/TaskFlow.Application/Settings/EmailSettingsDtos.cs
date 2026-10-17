using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Settings;

/// <summary>The email delivery configuration (Screen 14). There is deliberately no password field: the
/// password lives only in server configuration and the screen sees just <see cref="PasswordConfigured"/>.
/// <see cref="Issues"/> lists anything that would stop email from being sent.</summary>
public record EmailSettingsDto(
    bool EmailEnabled,
    string SmtpHost,
    int SmtpPort,
    SmtpSecurity SmtpSecurity,
    string SenderAddress,
    string SenderName,
    string SmtpUsername,
    string SendTime,
    bool PasswordConfigured,
    IReadOnlyList<string> Issues,
    DateTime? UpdatedAt,
    string? UpdatedByName);

/// <summary>A save of the email configuration. Value fields are nullable only so that an omitted field is a
/// validation error instead of silently binding to false/0; text fields may be null or empty.</summary>
public record UpdateEmailSettingsRequest(
    bool? EmailEnabled,
    string? SmtpHost,
    int? SmtpPort,
    SmtpSecurity? SmtpSecurity,
    string? SenderAddress,
    string? SenderName,
    string? SmtpUsername,
    string? SendTime);

/// <summary>A test email request; without an address the test goes to the caller's own email.</summary>
public record SendTestEmailRequest(string? ToAddress);

public record TestEmailResultDto(bool Success, string Message, int? DeliveryId);

/// <summary>One row of the email delivery log. The body is not returned.</summary>
public record EmailDeliveryDto(
    int Id,
    EmailKind Kind,
    string ToAddress,
    string Subject,
    int? TaskId,
    string? TaskTitle,
    EmailDeliveryStatus Status,
    int Attempts,
    string? LastError,
    DateTime CreatedAt,
    DateTime? LastAttemptAt,
    DateTime? NextAttemptAt,
    DateTime? SentAt);

/// <summary>Everything needed to talk to the SMTP server, password included. Internal to the server: it is
/// never serialized into a response, and <see cref="ToString"/> redacts the password.</summary>
public sealed record SmtpSettings(
    string Host,
    int Port,
    SmtpSecurity Security,
    string SenderAddress,
    string SenderName,
    string Username,
    string? Password)
{
    public override string ToString() =>
        $"SmtpSettings {{ Host = {Host}, Port = {Port}, Security = {Security}, SenderAddress = {SenderAddress}, " +
        $"SenderName = {SenderName}, Username = {Username}, Password = {(string.IsNullOrEmpty(Password) ? "(not set)" : "***")} }}";
}
