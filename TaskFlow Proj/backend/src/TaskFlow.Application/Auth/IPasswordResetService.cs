using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Auth;

/// <summary>Applies an administrator-initiated password reset: a generated password replaces the old one, every
/// existing session ends, the one-time token (when there is one) is consumed, and the user is notified.</summary>
public interface IPasswordResetService
{
    /// <summary>Assigns a newly generated password to the user, ends all of their sessions, consumes the given
    /// token when one is supplied, publishes an in-app notification, and returns the plaintext password so the
    /// caller can deliver it by email. The plaintext password is never stored or returned by the API.</summary>
    Task<string> ApplyGeneratedPasswordAsync(
        User user,
        PasswordResetToken? token,
        string notificationMessage,
        CancellationToken cancellationToken = default);
}
