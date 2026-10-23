using System.Security.Cryptography;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Auth;

/// <summary>Data access and token operations for password-reset tokens: insert, read by token hash,
/// mark used, generate, and validate.</summary>
public interface IPasswordResetTokenService
{
    /// <summary>Generates a crypto-random one-time token, stores its hash with a 5-minute expiry,
    /// and returns the plaintext token (only valid for the reset link).</summary>
    Task<string> RequestResetTokenAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Stores a pre-computed reset token for a user. Returns the stored entity.</summary>
    Task<PasswordResetToken> CreateAsync(int userId, string tokenHash, DateTime expiresAt, CancellationToken cancellationToken = default);

    /// <summary>Reads a token by its hash, including the user. Returns null when not found.</summary>
    Task<PasswordResetToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Marks a token as used and records when it was consumed.</summary>
    System.Threading.Tasks.Task MarkUsedAsync(PasswordResetToken token, DateTime consumedAt, CancellationToken cancellationToken = default);

    /// <summary>Validates a plaintext token: checks expiry first, then single-use. Returns the user
    /// when valid, or null when expired or already used.</summary>
    Task<User?> ValidateTokenAsync(string plaintextToken, CancellationToken cancellationToken = default);

    /// <summary>Reads the stored token for a plaintext token when it is still valid, so the caller can mark this
    /// exact token used. Returns null when the token is unknown, expired, or already used.</summary>
    Task<PasswordResetToken?> FindValidAsync(string plaintextToken, CancellationToken cancellationToken = default);
}
