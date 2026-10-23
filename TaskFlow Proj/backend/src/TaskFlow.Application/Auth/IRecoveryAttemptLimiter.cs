namespace TaskFlow.Application.Auth;

/// <summary>Throttles failed password-recovery attempts per account and per caller address. Recovery has its own budget so
/// that wrong guesses here can never lock the account's real sign-in.</summary>
public interface IRecoveryAttemptLimiter
{
    /// <summary>True when either budget is spent; the caller should refuse without checking the code.</summary>
    bool IsBlocked(string identifier, string? remoteAddress);

    /// <summary>Counts one failed attempt against both budgets.</summary>
    void RecordFailure(string identifier, string? remoteAddress);

    /// <summary>Clears the account's budget after a successful recovery step.</summary>
    void Reset(string identifier);
}
