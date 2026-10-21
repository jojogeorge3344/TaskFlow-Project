using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.PasswordReset;

/// <summary>Password recovery requests: creating one from either entry point (and notifying the
/// administrators), and listing them for the Employee Management screen. Implementations live in
/// Infrastructure because they write the request table and the system mail.</summary>
public interface IPasswordResetService
{
    /// <summary>Records a recovery request for one account and then notifies the administrators.
    /// Exactly one of <paramref name="sessionUserId"/> (Profile Screen, identity from the token) or
    /// <paramref name="identifier"/> (Login Screen, email address or username typed by the visitor)
    /// decides the account; the other is ignored. An identifier that matches no active, non-deleted
    /// account writes nothing and returns false - callers must not expose that difference.</summary>
    Task<bool> CreateRequestAsync(
        int? sessionUserId,
        string? identifier,
        PasswordResetOrigin origin,
        CancellationToken cancellationToken = default);

    /// <summary>All recovery requests newest first, with the requesting user's profile, for the
    /// administrator's list. Includes requests whose requester was deleted afterwards.</summary>
    Task<IReadOnlyList<PasswordResetRequestSummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>The administrator's processing action: changes the requesting user's password (generated
    /// for Login-Screen origins, supplied-or-generated for Profile-Screen ones), then notifies the user
    /// by mail and in-app notification, then marks the request Completed - in that order (design D8).
    /// A failed notification leaves the request Failed with a reason; the password stays changed.
    /// Returns NotFound for an unknown reference, AlreadyProcessed when the request is not Pending.</summary>
    Task<PasswordResetProcessResult> ProcessAsync(
        Guid referenceId,
        string? suppliedPassword,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    /// <summary>Re-runs only the notification half of a Failed request: no password is re-hashed and no
    /// temporary password is re-generated (design D8), so the request simply completes if the
    /// notification now succeeds.</summary>
    Task<PasswordResetProcessResult> RetryNotificationAsync(
        Guid referenceId,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    /// <summary>Mints the encrypted login-link token for a Completed request that is the account's latest
    /// reset event (design D14). Anything else is refused, so an earlier reset's link can never be obtained.</summary>
    Task<PasswordResetLinkResult> CreateLoginLinkAsync(Guid referenceId, CancellationToken cancellationToken = default);

    /// <summary>The email a valid login-link token resolves to, for pre-filling the Login Screen; null for a bad,
    /// expired or superseded token, which callers must treat exactly like an unknown account.</summary>
    Task<string?> ResolveLoginLinkAsync(string? token, CancellationToken cancellationToken = default);

    /// <summary>Updates the password from the Login Screen: verifies the temporary password the administrator
    /// issued, replaces it, lifts the must-change requirement, ends sessions and clears the notice. Returns false
    /// for every refusal (wrong password, unknown or inactive account, bad token, no pending change) without
    /// saying which.</summary>
    Task<bool> CompleteFromLoginAsync(PasswordResetCompleteRequest request, CancellationToken cancellationToken = default);
}
