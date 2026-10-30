namespace TaskFlow.Api.Integrations.Interfaces.Security;

/// <summary>Encrypts the shared MFA key for storage and issues the short-lived sign-in challenge. Neither value is
/// ever logged.</summary>
public interface IMfaSecretProtector
{
    string Protect(byte[] secret);

    /// <summary>The key, or null when the stored value is missing, tampered with or from another key ring.</summary>
    byte[]? Unprotect(string? protectedSecret);

    /// <summary>A tamper-proof token binding the user and their security stamp, valid for <paramref name="lifetime"/>.</summary>
    string CreateChallenge(int userId, string securityStamp, TimeSpan lifetime);

    /// <summary>The user id and stamp from a challenge, or null when it is forged, expired or malformed.</summary>
    (int UserId, string SecurityStamp)? ReadChallenge(string? challenge);
}
