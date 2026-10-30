namespace TaskFlow.Application.Assistant;

/// <summary>Limits how many chat requests one user can send per minute, so one user cannot use up a free-tier quota.</summary>
public interface IAssistantRateLimiter
{
    /// <summary>Records a request and returns null when it is allowed, or how long to wait when it is over the limit.</summary>
    TimeSpan? TryAcquire(int userId);
}
