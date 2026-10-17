namespace TaskFlow.Application.Settings;

/// <summary>When reminder emails go out: the shared email switch and the local time of day to send them.</summary>
public record EmailSchedule(bool Enabled, TimeOnly SendTime);

/// <summary>Reads and saves the email delivery configuration stored in the application settings table. The
/// SMTP password comes only from server configuration.</summary>
public interface IEmailSettingsService
{
    /// <summary>The configuration as the Email Configuration screen shows it (no password).</summary>
    Task<EmailSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves an already-validated request and returns the stored configuration.</summary>
    Task<EmailSettingsDto> UpdateAsync(int userId, UpdateEmailSettingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>The connection settings, password included, for sending. Never return this to a client.</summary>
    Task<SmtpSettings> GetSmtpSettingsAsync(CancellationToken cancellationToken = default);

    Task<EmailSchedule> GetScheduleAsync(CancellationToken cancellationToken = default);
}
