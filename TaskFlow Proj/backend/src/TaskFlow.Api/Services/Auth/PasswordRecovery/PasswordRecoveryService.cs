using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Auth.Mfa;
using TaskFlow.Api.Dtos.Auth.PasswordRecovery;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Repositories.Auth;
using TaskFlow.Api.Repositories.Auth.PasswordRecovery;
using TaskFlow.Api.Services.Auth;
using TaskFlow.Api.Services.Auth.Mfa;
using TaskFlow.Api.Services.Communication.Notification;

namespace TaskFlow.Api.Services.Auth.PasswordRecovery;

public class PasswordRecoveryService : IPasswordRecoveryService
{
    /// <summary>How long a reset token stays valid after the code was accepted.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);

    private readonly IAccountRepository _accounts;
    private readonly IPasswordResetGrantRepository _grants;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMfaService _mfaService;
    private readonly IAttemptLimiter _limiter;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<PasswordRecoveryService> _logger;
    private readonly TimeProvider _timeProvider;

    public PasswordRecoveryService(
        IAccountRepository accounts,
        IPasswordResetGrantRepository grants,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        IMfaService mfaService,
        IAttemptLimiter limiter,
        IPasswordHasher<User> passwordHasher,
        INotificationPublisher notificationPublisher,
        ILogger<PasswordRecoveryService> logger,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _grants = grants;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _mfaService = mfaService;
        _limiter = limiter;
        _passwordHasher = passwordHasher;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<RecoveryVerifyResult> VerifyAuthenticatorAsync(
        string identifier,
        string? code,
        string? remoteAddress,
        CancellationToken cancellationToken = default)
    {
        identifier = identifier.Trim();

        // A caller whose address is already over budget is turned away before any database work.
        if (_limiter.IsAddressBlocked(remoteAddress))
        {
            return new RecoveryVerifyResult(RecoveryVerifyStatus.Throttled);
        }

        var user = await _accounts.FindActiveByLoginForUpdateAsync(identifier, cancellationToken);

        // The budget follows the account (not the spelling typed, so username and email share it) and the caller's address.
        // The attempt is reserved before the code is checked, so a burst of parallel requests cannot all slip through. Wrong
        // codes here never touch the account's sign-in lockout, so a stranger who only knows a username cannot lock the owner
        // out. A name that matches nothing is throttled as well, and every refusal looks the same.
        var accountKey = user is null ? IAttemptLimiter.UnknownKey(identifier) : IAttemptLimiter.AccountKey(user.Id);
        if (!_limiter.TryAcquire(accountKey, remoteAddress))
        {
            return new RecoveryVerifyResult(RecoveryVerifyStatus.Throttled);
        }

        if (user is null || user.MfaEnabledAt is null)
        {
            return new RecoveryVerifyResult(RecoveryVerifyStatus.Failed);
        }

        // An unused backup code is not a recovery factor. The account's own lock still applies.
        var outcome = await _mfaService.VerifyAsync(user, code, allowBackupCode: false, countFailure: false, cancellationToken: cancellationToken);
        if (outcome != MfaVerifyOutcome.Success)
        {
            return new RecoveryVerifyResult(RecoveryVerifyStatus.Failed);
        }

        _limiter.Reset(accountKey);
        return new RecoveryVerifyResult(RecoveryVerifyStatus.Verified, await IssueResetTokenAsync(user.Id, cancellationToken));
    }

    public async Task<string> IssueResetTokenAsync(int userId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        _grants.Add(new PasswordResetGrant
        {
            UserId = userId,
            TokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = now.Add(TokenLifetime)
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return token;
    }

    public async Task<bool> ResetPasswordAsync(string resetToken, string newPassword, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var grant = await _grants.FindUnusedByHashWithUserForUpdateAsync(HashToken(resetToken), cancellationToken);

        if (grant?.User is null || grant.ExpiresAt <= now || grant.User.IsDeleted || !grant.User.IsActive)
        {
            return false;
        }

        var user = grant.User;
        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        // A new stamp makes every access token already handed out fail session validation.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.UpdatedAt = now;
        grant.UsedAt = now;

        foreach (var activeToken in await _refreshTokens.GetActiveForUsersForUpdateAsync([user.Id], cancellationToken))
        {
            activeToken.RevokedAt = now;
        }

        // Any other token still outstanding for this account dies with the reset.
        foreach (var other in await _grants.GetOtherUnusedForUpdateAsync(user.Id, grant.Id, cancellationToken))
        {
            other.UsedAt = now;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // A parallel request spent the same token first.
            return false;
        }

        // A recovery needs only the authenticator, so the owner is told straight away if it was not them. The password has
        // already changed, so a failure to write the notice must not turn this into an error the caller would retry.
        try
        {
            await _notificationPublisher.PublishAsync(
                user.Id,
                taskId: null,
                NotificationType.System,
                "Your password was reset with your authenticator app. If this was not you, contact an administrator now.",
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not record the password-recovery notification for user {UserId}.", user.Id);
        }

        return true;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
