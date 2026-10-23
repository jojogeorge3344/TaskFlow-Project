using System.Collections.Concurrent;
using TaskFlow.Application.Auth;

namespace TaskFlow.Infrastructure.Services;

/// <summary>A sliding window in memory, kept per requesting account and per Administrator recipient. A restart
/// resets it, which is acceptable for a safeguard against reset-mail flooding.</summary>
public class PasswordResetRateLimiter : IPasswordResetRateLimiter
{
    public const int RequestsPerUser = 3;
    public const int RequestsPerAdministrator = 10;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _requests = new();

    public PasswordResetRateLimiter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public bool TryAcquire(int userId, int? administratorId)
    {
        var now = _timeProvider.GetUtcNow();

        // Check both budgets before recording either, so a refused request spends nothing.
        if (administratorId is { } administrator &&
            Count("administrator:" + administrator, now) >= RequestsPerAdministrator)
        {
            return false;
        }

        if (Count("user:" + userId, now) >= RequestsPerUser)
        {
            return false;
        }

        Record("user:" + userId, now);

        if (administratorId is { } recipient)
        {
            Record("administrator:" + recipient, now);
        }

        return true;
    }

    private int Count(string key, DateTimeOffset now)
    {
        var queue = _requests.GetOrAdd(key, _ => new Queue<DateTimeOffset>());

        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() >= Window)
            {
                queue.Dequeue();
            }

            return queue.Count;
        }
    }

    private void Record(string key, DateTimeOffset now)
    {
        var queue = _requests.GetOrAdd(key, _ => new Queue<DateTimeOffset>());

        lock (queue)
        {
            queue.Enqueue(now);
        }
    }
}
