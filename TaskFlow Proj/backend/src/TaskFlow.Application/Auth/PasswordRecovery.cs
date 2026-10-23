using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Users.Validators;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Application.Auth;

/// <summary>Step 2 of recovery from the login page: the single-use token from the authenticator step plus the new password.</summary>
public record RecoverPasswordRequest(string? ResetToken, string? NewPassword, string? ConfirmPassword);

/// <summary>Returned after a correct authenticator code; the token is needed for the last step and works once.</summary>
public record RecoveryTokenResponse(string ResetToken);

public class RecoverPasswordRequestValidator : AbstractValidator<RecoverPasswordRequest>
{
    public RecoverPasswordRequestValidator()
    {
        RuleFor(x => x.ResetToken).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(ChangePasswordRequestValidator.MinPasswordLength)
            .MaximumLength(ChangePasswordRequestValidator.MaxPasswordLength);

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

/// <summary>Issues and spends the reset token behind "forgot password" on the login page.</summary>
public interface IPasswordRecoveryService
{
    /// <summary>Mints the single-use token for a user who already proved themselves (with an authenticator code).</summary>
    Task<string> IssueResetTokenAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Sets the new password with a reset token and ends the user's sessions. Returns false when the token is
    /// unknown, used or expired.</summary>
    Task<bool> ResetPasswordAsync(string resetToken, string newPassword, CancellationToken cancellationToken = default);
}

public class PasswordRecoveryService : IPasswordRecoveryService
{
    /// <summary>How long a reset token stays valid after the code was accepted.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<PasswordRecoveryService> _logger;
    private readonly TimeProvider _timeProvider;

    public PasswordRecoveryService(
        IApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        INotificationPublisher notificationPublisher,
        ILogger<PasswordRecoveryService> logger,
        TimeProvider timeProvider)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<string> IssueResetTokenAsync(int userId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        _db.PasswordResetGrants.Add(new PasswordResetGrant
        {
            UserId = userId,
            TokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = now.Add(TokenLifetime)
        });
        await _db.SaveChangesAsync(cancellationToken);

        return token;
    }

    public async Task<bool> ResetPasswordAsync(string resetToken, string newPassword, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(resetToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var grant = await _db.PasswordResetGrants
            .Include(row => row.User)
            .FirstOrDefaultAsync(row => row.TokenHash == tokenHash && row.UsedAt == null, cancellationToken);

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

        var activeTokens = await _db.RefreshTokens
            .Where(stored => stored.UserId == user.Id && stored.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var activeToken in activeTokens)
        {
            activeToken.RevokedAt = now;
        }

        // Any other token still outstanding for this account dies with the reset.
        var others = await _db.PasswordResetGrants
            .Where(row => row.UserId == user.Id && row.Id != grant.Id && row.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var other in others)
        {
            other.UsedAt = now;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
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
