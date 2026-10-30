using Task = System.Threading.Tasks.Task;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Mfa;

public enum MfaVerifyOutcome
{
    Success,
    Invalid,
    /// <summary>Too many recent wrong codes; even a correct code is refused until the lock ends.</summary>
    LockedOut
}

/// <summary>What setup returns once: the key to type in and the URI behind the QR code.</summary>
public record MfaSetupResult(string Key, string ProvisioningUri);

/// <summary>Authenticator-app MFA: enrolment, code checking shared by every entry point, backup codes, and reset.
/// Operations that need a password or code throw <see cref="InvalidOperationException"/> with a message safe to show.</summary>
public interface IMfaService
{
    /// <summary>Checks the current password, issues a fresh key (replacing any unconfirmed one) and returns it. MFA stays
    /// off until <see cref="EnableAsync"/> succeeds.</summary>
    Task<MfaSetupResult?> BeginSetupAsync(int userId, string currentPassword, CancellationToken cancellationToken = default);

    /// <summary>Confirms setup with a valid code, turns MFA on and returns the backup codes, to be shown once.</summary>
    Task<IReadOnlyList<string>?> EnableAsync(int userId, string code, CancellationToken cancellationToken = default);

    /// <summary>Replaces the backup codes after the password and a valid code and returns the new set.</summary>
    Task<IReadOnlyList<string>?> RegenerateBackupCodesAsync(int userId, string currentPassword, string code, CancellationToken cancellationToken = default);

    /// <summary>Step-up for a sensitive action (for example changing the password). When the user has MFA on, a valid code is
    /// required, and it is spent like at sign-in. Throws <see cref="InvalidOperationException"/> when the code is missing, wrong
    /// or the account is locked. Does nothing for a user who has no MFA (only possible while <c>Mfa:Required</c> is off).</summary>
    Task RequireCodeAsync(int userId, string? code, CancellationToken cancellationToken = default);

    /// <summary>Checks an authenticator code without spending it, to unlock something cosmetic such as showing a typed password.
    /// Wrong codes still count toward the lockout. A backup code is not accepted.</summary>
    Task<MfaVerifyOutcome> CheckCodeAsync(int userId, string? code, CancellationToken cancellationToken = default);

    /// <summary>Checks an authenticator code, or (when <paramref name="allowBackupCode"/>) an unused backup code, applying
    /// the replay rule and the shared lockout. Saves the counters. With <paramref name="countFailure"/> false a wrong code is
    /// refused without touching the account's failed-attempt counter, for callers that throttle by themselves (password recovery).</summary>
    Task<MfaVerifyOutcome> VerifyAsync(User user, string? code, bool allowBackupCode, bool countFailure = true, CancellationToken cancellationToken = default);

    /// <summary>Administrator reset: removes the key and backup codes, ends the user's sessions and notifies them.
    /// Returns false when the user does not exist.</summary>
    Task<bool> ResetAsync(int userId, CancellationToken cancellationToken = default);
}
