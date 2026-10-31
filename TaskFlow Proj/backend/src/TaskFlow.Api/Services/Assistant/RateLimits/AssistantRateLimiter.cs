using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Common.Exceptions;
using System.Collections.Concurrent;

namespace TaskFlow.Api.Services.Assistant.RateLimits;

/// <summary>A sliding one-minute window per user, kept in memory. A restart resets it, which is fine for a safeguard
/// against free-tier quota use.</summary>
public class AssistantRateLimiter : IAssistantRateLimiter
{
    public const int RequestsPerMinute = 20;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<int, Queue<DateTimeOffset>> _requests = new();

    public AssistantRateLimiter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public TimeSpan? TryAcquire(int userId)
    {
        var now = _timeProvider.GetUtcNow();
        var queue = _requests.GetOrAdd(userId, _ => new Queue<DateTimeOffset>());

        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() >= Window)
            {
                queue.Dequeue();
            }

            if (queue.Count >= RequestsPerMinute)
            {
                return Window - (now - queue.Peek());
            }

            queue.Enqueue(now);
            return null;
        }
    }
}
