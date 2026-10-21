namespace TaskFlow.Application.PasswordReset;

/// <summary>Encrypted, expiring tokens for the per-user login address (design D14). A token carries the user
/// and the reset request it was minted for, is tamper-evident, and contains neither a readable identifier nor
/// any password.</summary>
public interface IPasswordResetLinkTokens
{
    /// <summary>Mints a url-safe token for one user and one reset request.</summary>
    string Create(int userId, int requestId);

    /// <summary>The user and request a token names, or null when it is malformed, tampered with or expired.</summary>
    (int UserId, int RequestId)? Read(string? token);
}
