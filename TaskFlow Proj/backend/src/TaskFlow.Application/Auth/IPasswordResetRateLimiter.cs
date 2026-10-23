namespace TaskFlow.Application.Auth;

/// <summary>Throttles forgot-password requests per requesting account and per Administrator recipient,
/// so a throttled response looks identical to a successful one (no user enumeration).</summary>
public interface IPasswordResetRateLimiter
{
    /// <summary>Attempts to record a request. Returns true when allowed, false when throttled.</summary>
    bool TryAcquire(int userId, int? administratorId);
}
