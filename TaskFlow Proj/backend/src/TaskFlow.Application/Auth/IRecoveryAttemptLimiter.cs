namespace TaskFlow.Application.Auth;

/// <summary>Throttles failed password-recovery attempts per account and per caller address. Recovery has its own budget so
/// that wrong guesses here can never lock the account's real sign-in.</summary>
public interface IRecoveryAttemptLimiter
{
    /// <summary>The budget key for an account that exists: the same whether the caller typed the username or the email.</summary>
    static string AccountKey(int userId) => "user:" + userId;

    /// <summary>The budget key for a name that matches no account, so guessing names is throttled too.</summary>
    static string UnknownKey(string identifier) => "name:" + identifier.Trim().ToUpperInvariant();

    /// <summary>True when either budget is spent; the caller should refuse without checking the code.</summary>
    bool IsBlocked(string accountKey, string? remoteAddress);

    /// <summary>Counts one failed attempt against both budgets.</summary>
    void RecordFailure(string accountKey, string? remoteAddress);

    /// <summary>Clears the account's budget after a successful recovery step.</summary>
    void Reset(string accountKey);
}
