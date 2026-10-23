using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Data access and token operations for password-reset tokens using the application database context.</summary>
public class PasswordResetTokenService : IPasswordResetTokenService
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public PasswordResetTokenService(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <summary>Generates a crypto-random one-time token, stores its hash with a 5-minute expiry,
    /// and returns the plaintext token (only valid for the reset link).</summary>
    public async Task<string> RequestResetTokenAsync(int userId, CancellationToken cancellationToken = default)
    {
        var plaintextToken = GenerateToken();
        var tokenHash = HashToken(plaintextToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(5);

        var token = new PasswordResetToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            Used = false
        };

        _dbContext.PasswordResetTokens.Add(token);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return plaintextToken;
    }

    public async Task<PasswordResetToken> CreateAsync(
        int userId,
        string tokenHash,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var token = new PasswordResetToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            Used = false
        };

        _dbContext.PasswordResetTokens.Add(token);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<PasswordResetToken?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PasswordResetTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async System.Threading.Tasks.Task MarkUsedAsync(
        PasswordResetToken token,
        DateTime consumedAt,
        CancellationToken cancellationToken = default)
    {
        token.Used = true;
        token.ConsumedAt = consumedAt;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Validates a plaintext token: checks expiry first, then single-use. Returns the user
    /// when valid, or null when expired or already used.</summary>
    public async Task<User?> ValidateTokenAsync(string plaintextToken, CancellationToken cancellationToken = default)
    {
        var token = await FindValidAsync(plaintextToken, cancellationToken);

        return token?.User;
    }

    public async Task<PasswordResetToken?> FindValidAsync(
        string plaintextToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(plaintextToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var token = await _dbContext.PasswordResetTokens
            .Include(stored => stored.User)
            .FirstOrDefaultAsync(stored => stored.TokenHash == tokenHash, cancellationToken);

        if (token is null)
        {
            return null;
        }

        // Expiry is checked before single-use so an aged-out link reports "expired", not "link not valid".
        if (token.ExpiresAt <= now)
        {
            return null;
        }

        return token.Used ? null : token;
    }

    private static string GenerateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        // URL-safe Base64: the token travels in a reset link and a route segment, so '/' and '+' are avoided.
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }
}
