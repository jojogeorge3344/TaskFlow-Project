namespace TaskFlow.Api.Services.Interfaces.Auth;

/// <summary>Throttles guessable secrets (an authenticator code in password recovery, a re-entered current password) per
/// account and per caller address. It has its own budget, so wrong guesses here can never lock the account's real sign-in.
/// An attempt is reserved <em>before</em> the secret is checked, so parallel requests cannot all slip past the limit.</summary>
public interface IAttemptLimiter
{
    /// <summary>The budget key for an account that exists: the same whether the caller typed the username or the email.</summary>
    static string AccountKey(int userId) => "user:" + userId;

    /// <summary>The budget key for a name that matches no account, so guessing names is throttled too.</summary>
    static string UnknownKey(string identifier) => "name:" + identifier.Trim().ToUpperInvariant();

    /// <summary>The budget key for re-entering the current password while signed in (kept apart from recovery).</summary>
    static string PasswordKey(int userId) => "pwd:" + userId;

    /// <summary>A cheap read-only look at the address budget alone, so a caller that is already refused can be turned away before any
    /// database work. It reserves nothing; <see cref="TryAcquire"/> still decides atomically.</summary>
    bool IsAddressBlocked(string? remoteAddress);

    /// <summary>Reserves one attempt. Returns false, without checking anything, when the account or address budget is spent.</summary>
    bool TryAcquire(string accountKey, string? remoteAddress);

    /// <summary>Clears the account's budget after the secret was right.</summary>
    void Reset(string accountKey);
}
