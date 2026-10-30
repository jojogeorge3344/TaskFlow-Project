using TaskFlow.Api.Dtos.Auth.PasswordRecovery;

namespace TaskFlow.Api.Services.Interfaces.Auth.PasswordRecovery;

/// <summary>Issues and spends the reset token behind "forgot password" on the login page.</summary>
public interface IPasswordRecoveryService
{
    /// <summary>Step 1: checks an authenticator code for the account named by <paramref name="identifier"/>, throttled by
    /// account and by <paramref name="remoteAddress"/>, and on success returns a single-use reset token.</summary>
    Task<RecoveryVerifyResult> VerifyAuthenticatorAsync(
        string identifier,
        string? code,
        string? remoteAddress,
        CancellationToken cancellationToken = default);

    /// <summary>Mints the single-use token for a user who already proved themselves (with an authenticator code).</summary>
    Task<string> IssueResetTokenAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Sets the new password with a reset token and ends the user's sessions. Returns false when the token is
    /// unknown, used or expired.</summary>
    Task<bool> ResetPasswordAsync(string resetToken, string newPassword, CancellationToken cancellationToken = default);
}
