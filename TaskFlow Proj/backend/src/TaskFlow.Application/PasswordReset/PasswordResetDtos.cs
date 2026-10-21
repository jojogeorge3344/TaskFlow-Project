using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.PasswordReset;

/// <summary>The anonymous Forgot Password submission: one identifier, nothing else.</summary>
public record PasswordResetSubmission(string? Identifier);

/// <summary>The confirmation every submission gets. The same text comes back whether or not the
/// identifier matched an account, so the answer never discloses account existence.</summary>
public static class PasswordResetTexts
{
    public const string Confirmation =
        "If that email address or username belongs to an active account, a password recovery request " +
        "has been recorded and the administrator will be notified. The password will be updated only " +
        "by an administrator.";
}

/// <summary>One row of the administrator's Password reset requests list. Carries no password material
/// of any kind - only who asked, from where, what happened and when.</summary>
public record PasswordResetRequestSummary(
    Guid ReferenceId,
    int UserId,
    string FullName,
    string Username,
    string Email,
    string RoleName,
    PasswordResetOrigin Origin,
    PasswordResetStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    bool AdminNotified,
    bool UserNotified,
    bool TemporaryPasswordIssued,
    string? FailureReason);

/// <summary>What the administrator's process or retry attempt ended as. Deliberately carries no
/// password: the response must be safe to display and to log (design D4).</summary>
public enum PasswordResetProcessOutcome
{
    Completed,
    NotificationFailed,
    InvalidPassword,
    AlreadyProcessed,
    AccountUnavailable,
    NotFound
}

public record PasswordResetProcessResult(PasswordResetProcessOutcome Outcome, string? Reason = null);

/// <summary>The administrator's processing body: the password to set. Null or empty means "generate a
/// temporary password", which is the only choice for Login-Screen-originated requests.</summary>
public record PasswordResetProcessRequest(string? Password);

public enum PasswordResetLinkOutcome
{
    Ok,
    NotFound,

    /// <summary>The request is not Completed, or a later reset has replaced it as the account's latest event.</summary>
    NotLatestCompleted
}

/// <summary>The encrypted login-link token for a completed request. The token holds no password and no readable
/// identifier (design D14).</summary>
public record PasswordResetLinkResult(PasswordResetLinkOutcome Outcome, string? Token = null);

/// <summary>The password update from the Login Screen. The encrypted login-link token is the only proof and names the
/// account; without a valid one nothing changes (design D14). No identifier and no temporary password travel here.</summary>
public record PasswordResetCompleteRequest(string? Token, string? NewPassword);
