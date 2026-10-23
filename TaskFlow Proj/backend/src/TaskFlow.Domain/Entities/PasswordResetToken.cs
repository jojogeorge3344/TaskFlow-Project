namespace TaskFlow.Domain.Entities;

/// <summary>A one-time, time-limited password-reset token requested through the forgot-password flow.</summary>
public class PasswordResetToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool Used { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public User? User { get; set; }
}
