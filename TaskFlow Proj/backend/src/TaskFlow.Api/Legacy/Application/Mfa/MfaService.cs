using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Application.Mfa;

public class MfaService : IMfaService
{
    public const string WrongPasswordMessage = "Current password is incorrect.";
    public const string InvalidCodeMessage = "The code is incorrect or has expired.";
    public const string CodeRequiredMessage = "Enter the code from your authenticator app.";
    public const string LockedMessage = "Too many wrong codes. Try again in a few minutes.";

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IMfaSecretProtector _protector;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly MfaOptions _options;
    private readonly TimeProvider _timeProvider;

    public MfaService(
        IApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        IMfaSecretProtector protector,
        INotificationPublisher notificationPublisher,
        IOptions<MfaOptions> options,
        TimeProvider timeProvider)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _protector = protector;
        _notificationPublisher = notificationPublisher;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<MfaSetupResult?> BeginSetupAsync(int userId, string currentPassword, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        RequirePassword(user, currentPassword);

        if (user.MfaEnabledAt is not null)
        {
            throw new InvalidOperationException("Authenticator MFA is already on. Turn it off first to set it up again.");
        }

        var secret = Totp.NewSecret();
        user.MfaSecretProtected = _protector.Protect(secret);
        user.MfaLastUsedStep = 0;
        user.MfaFailedAttempts = 0;
        user.MfaLockedUntil = null;
        await _db.SaveChangesAsync(cancellationToken);

        return new MfaSetupResult(
            Base32.Encode(secret),
            Totp.ProvisioningUri(_options.Issuer, user.Email, secret));
    }

    public async Task<IReadOnlyList<string>?> EnableAsync(int userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (user.MfaEnabledAt is not null)
        {
            throw new InvalidOperationException("Authenticator MFA is already on.");
        }

        if (string.IsNullOrEmpty(user.MfaSecretProtected))
        {
            throw new InvalidOperationException("Start the setup first to get a key.");
        }

        RequireOutcome(await VerifyAsync(user, code, allowBackupCode: false, cancellationToken: cancellationToken));

        user.MfaEnabledAt = _timeProvider.GetUtcNow().UtcDateTime;
        var codes = await ReplaceBackupCodesAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return codes;
    }

    public async Task<IReadOnlyList<string>?> RegenerateBackupCodesAsync(int userId, string currentPassword, string code, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        RequirePassword(user, currentPassword);

        if (user.MfaEnabledAt is null)
        {
            throw new InvalidOperationException("Authenticator MFA is not on.");
        }

        RequireOutcome(await VerifyAsync(user, code, allowBackupCode: true, cancellationToken: cancellationToken));

        var codes = await ReplaceBackupCodesAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return codes;
    }

    public async Task RequireCodeAsync(int userId, string? code, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken);
        if (user is null || user.MfaEnabledAt is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException(CodeRequiredMessage);
        }

        RequireOutcome(await VerifyAsync(user, code, allowBackupCode: true, cancellationToken: cancellationToken));
    }

    public async Task<MfaVerifyOutcome> CheckCodeAsync(int userId, string? code, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken);
        if (user is null || user.MfaEnabledAt is null)
        {
            return MfaVerifyOutcome.Invalid;
        }

        return await VerifyCoreAsync(user, code, allowBackupCode: false, consumeCode: false, countFailure: true, cancellationToken);
    }

    public Task<MfaVerifyOutcome> VerifyAsync(User user, string? code, bool allowBackupCode, bool countFailure = true, CancellationToken cancellationToken = default) =>
        VerifyCoreAsync(user, code, allowBackupCode, consumeCode: true, countFailure, cancellationToken);

    private async Task<MfaVerifyOutcome> VerifyCoreAsync(User user, string? code, bool allowBackupCode, bool consumeCode, bool countFailure, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        if (user.MfaLockedUntil is { } lockedUntil && lockedUntil > now.UtcDateTime)
        {
            return MfaVerifyOutcome.LockedOut;
        }

        var accepted = false;
        var trimmed = code?.Trim();

        if (trimmed is { Length: Totp.Digits } && trimmed.All(char.IsAsciiDigit))
        {
            var secret = _protector.Unprotect(user.MfaSecretProtected);
            // A spent code can never be used again, except for the cosmetic check, which leaves the counter alone.
            if (secret is not null && Totp.TryValidate(secret, trimmed, now, consumeCode ? user.MfaLastUsedStep : 0, out var step))
            {
                if (consumeCode)
                {
                    user.MfaLastUsedStep = step;
                }

                accepted = true;
            }
        }
        else if (allowBackupCode && NormalizeBackupCode(trimmed) is { } normalized)
        {
            var hash = HashBackupCode(normalized);
            var stored = await _db.MfaBackupCodes.FirstOrDefaultAsync(
                row => row.UserId == user.Id && row.UsedAt == null && row.CodeHash == hash,
                cancellationToken);
            if (stored is not null)
            {
                stored.UsedAt = now.UtcDateTime;
                accepted = true;
            }
        }

        if (accepted)
        {
            user.MfaFailedAttempts = 0;
            user.MfaLockedUntil = null;
            return await TrySaveAsync(MfaVerifyOutcome.Success, cancellationToken);
        }

        if (!countFailure)
        {
            return MfaVerifyOutcome.Invalid;
        }

        user.MfaFailedAttempts++;
        if (user.MfaFailedAttempts >= _options.MaxFailedAttempts)
        {
            // The lock starts a fresh count when it ends.
            user.MfaLockedUntil = now.UtcDateTime.AddMinutes(_options.LockoutMinutes);
            user.MfaFailedAttempts = 0;
        }

        await TrySaveAsync(MfaVerifyOutcome.Invalid, cancellationToken);
        return MfaVerifyOutcome.Invalid;
    }

    /// <summary>Saves the counters. The step, attempt counter and backup-code state are concurrency tokens, so when a parallel
    /// request already spent the same code or moved the counters, this save loses and the code counts as refused instead of
    /// being accepted twice.</summary>
    private async Task<MfaVerifyOutcome> TrySaveAsync(MfaVerifyOutcome onSuccess, CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return onSuccess;
        }
        catch (DbUpdateConcurrencyException)
        {
            return MfaVerifyOutcome.Invalid;
        }
    }

    public async Task<bool> ResetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await ClearAsync(user, cancellationToken);

        // Ending the sessions makes the user sign in again, so a stolen session cannot outlive the reset.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.UpdatedAt = now;
        var activeTokens = await _db.RefreshTokens
            .Where(stored => stored.UserId == user.Id && stored.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _notificationPublisher.PublishAsync(
            user.Id,
            taskId: null,
            NotificationType.System,
            "An administrator reset your authenticator MFA. Sign in with your password and set it up again from Profile.",
            cancellationToken);

        return true;
    }

    private Task<User?> FindAsync(int userId, CancellationToken cancellationToken) =>
        _db.Users.FirstOrDefaultAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);

    private void RequirePassword(User user, string? password)
    {
        if (string.IsNullOrEmpty(password)
            || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            throw new InvalidOperationException(WrongPasswordMessage);
        }
    }

    private static void RequireOutcome(MfaVerifyOutcome outcome)
    {
        switch (outcome)
        {
            case MfaVerifyOutcome.LockedOut:
                throw new InvalidOperationException(LockedMessage);
            case MfaVerifyOutcome.Invalid:
                throw new InvalidOperationException(InvalidCodeMessage);
        }
    }

    private async Task ClearAsync(User user, CancellationToken cancellationToken)
    {
        user.MfaSecretProtected = null;
        user.MfaEnabledAt = null;
        user.MfaLastUsedStep = 0;
        user.MfaFailedAttempts = 0;
        user.MfaLockedUntil = null;

        var codes = await _db.MfaBackupCodes.Where(row => row.UserId == user.Id).ToListAsync(cancellationToken);
        _db.MfaBackupCodes.RemoveRange(codes);
    }

    /// <summary>Deletes the old set and creates a new one. Returns the codes in their displayed form (XXXXX-XXXXX).</summary>
    private async Task<IReadOnlyList<string>> ReplaceBackupCodesAsync(User user, CancellationToken cancellationToken)
    {
        var old = await _db.MfaBackupCodes.Where(row => row.UserId == user.Id).ToListAsync(cancellationToken);
        _db.MfaBackupCodes.RemoveRange(old);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var shown = new List<string>(_options.BackupCodeCount);
        for (var i = 0; i < _options.BackupCodeCount; i++)
        {
            var raw = Base32.Encode(RandomNumberGenerator.GetBytes(7))[..10];
            shown.Add($"{raw[..5]}-{raw[5..]}");
            _db.MfaBackupCodes.Add(new MfaBackupCode { UserId = user.Id, CodeHash = HashBackupCode(raw), CreatedAt = now });
        }

        return shown;
    }

    /// <summary>Upper-cases and strips dashes and spaces; null unless the result is 10 Base32 characters.</summary>
    private static string? NormalizeBackupCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var cleaned = new string(code.Where(c => c is not ('-' or ' ')).Select(char.ToUpperInvariant).ToArray());
        return cleaned.Length == 10 && Base32.Decode(cleaned) is not null ? cleaned : null;
    }

    private static string HashBackupCode(string normalized) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
}
