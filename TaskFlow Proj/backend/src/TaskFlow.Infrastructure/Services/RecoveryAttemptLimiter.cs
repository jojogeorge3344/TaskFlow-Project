using TaskFlow.Application.Auth;

namespace TaskFlow.Infrastructure.Services;

/// <summary>A sliding window in memory (a restart resets it). One lock covers every operation, so a sweep can never drop a
/// failure another request is recording; the traffic here is a handful of calls per minute. Idle keys are swept away so the
/// table cannot grow without bound.</summary>
public class RecoveryAttemptLimiter : IRecoveryAttemptLimiter
{
    public const int FailuresPerAccount = 5;
    public const int FailuresPerAddress = 20;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _failures = new();
    private DateTimeOffset _lastSweep;

    public RecoveryAttemptLimiter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _lastSweep = timeProvider.GetUtcNow();
    }

    public bool IsBlocked(string accountKey, string? remoteAddress)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            return Count(accountKey, now) >= FailuresPerAccount
                || (remoteAddress is not null && Count(AddressKey(remoteAddress), now) >= FailuresPerAddress);
        }
    }

    public void RecordFailure(string accountKey, string? remoteAddress)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            Sweep(now);
            Record(accountKey, now);
            if (remoteAddress is not null)
            {
                Record(AddressKey(remoteAddress), now);
            }
        }
    }

    public void Reset(string accountKey)
    {
        lock (_gate)
        {
            _failures.Remove(accountKey);
        }
    }

    private static string AddressKey(string address) => "ip:" + address;

    private int Count(string key, DateTimeOffset now)
    {
        if (!_failures.TryGetValue(key, out var queue))
        {
            return 0;
        }

        while (queue.Count > 0 && now - queue.Peek() >= Window)
        {
            queue.Dequeue();
        }

        return queue.Count;
    }

    private void Record(string key, DateTimeOffset now)
    {
        if (!_failures.TryGetValue(key, out var queue))
        {
            queue = new Queue<DateTimeOffset>();
            _failures[key] = queue;
        }

        queue.Enqueue(now);
    }

    private void Sweep(DateTimeOffset now)
    {
        if (now - _lastSweep < Window)
        {
            return;
        }

        _lastSweep = now;
        foreach (var key in _failures.Keys.ToList())
        {
            if (Count(key, now) == 0)
            {
                _failures.Remove(key);
            }
        }
    }
}
