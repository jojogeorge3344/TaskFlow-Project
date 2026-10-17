using TaskFlow.Application.Settings;

namespace TaskFlow.Application.Email;

/// <summary>Test emails and the email delivery log shown on the Email Configuration screen.</summary>
public interface IEmailDeliveryService
{
    public const int DefaultRecentCount = 20;
    public const int MaxRecentCount = 100;

    /// <summary>Sends a test email with the saved configuration (even while email notifications are disabled)
    /// and records it in the delivery log. Goes to <paramref name="toAddress"/>, or to the caller's own email
    /// when that is empty. Returns null when there is no address to send to.</summary>
    Task<TestEmailResultDto?> SendTestAsync(int userId, string? toAddress, CancellationToken cancellationToken = default);

    /// <summary>The newest deliveries first; <paramref name="take"/> is clamped to 1–<see cref="MaxRecentCount"/>.</summary>
    Task<IReadOnlyList<EmailDeliveryDto>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
