using System.Collections.Concurrent;
using TaskFlow.Application.Auth;

namespace TaskFlow.Infrastructure.Services;

/// <summary>A sliding window in memory (a restart resets it). Entries that have aged out are dropped as they are touched, and a
/// periodic sweep removes idle keys so the table cannot grow without bound.</summary>
public class RecoveryAttemptLimiter : IRecoveryAttemptLimiter
{
    public const int FailuresPerIdentifier = 5;
    public const int FailuresPerAddress = 20;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _failures = new();
    private DateTimeOffset _lastSweep;

    public RecoveryAttemptLimiter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _lastSweep = timeProvider.GetUtcNow();
    }

    public bool IsBlocked(string identifier, string? remoteAddress)
    {
        var now = _timeProvider.GetUtcNow();
        return Count(IdentifierKey(identifier), now) >= FailuresPerIdentifier
            || (remoteAddress is not null && Count(AddressKey(remoteAddress), now) >= FailuresPerAddress);
    }

    public void RecordFailure(string identifier, string? remoteAddress)
    {
        var now = _timeProvider.GetUtcNow();
        Sweep(now);
        Record(IdentifierKey(identifier), now);
        if (remoteAddress is not null)
        {
            Record(AddressKey(remoteAddress), now);
        }
    }

    public void Reset(string identifier) => _failures.TryRemove(IdentifierKey(identifier), out _);

    private static string IdentifierKey(string identifier) => "id:" + identifier.Trim().ToUpperInvariant();

    private static string AddressKey(string address) => "ip:" + address;

    private int Count(string key, DateTimeOffset now)
    {
        if (!_failures.TryGetValue(key, out var queue))
        {
            return 0;
        }

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
        var queue = _failures.GetOrAdd(key, _ => new Queue<DateTimeOffset>());
        lock (queue)
        {
            queue.Enqueue(now);
        }
    }

    private void Sweep(DateTimeOffset now)
    {
        if (now - _lastSweep < Window)
        {
            return;
        }

        _lastSweep = now;
        foreach (var key in _failures.Keys)
        {
            if (Count(key, now) == 0)
            {
                _failures.TryRemove(key, out _);
            }
        }
    }
}
