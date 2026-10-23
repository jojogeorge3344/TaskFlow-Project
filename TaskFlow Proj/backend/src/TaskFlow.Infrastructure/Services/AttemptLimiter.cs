using System.Net;
using System.Net.Sockets;
using TaskFlow.Application.Auth;

namespace TaskFlow.Infrastructure.Services;

/// <summary>A sliding window in memory (a restart resets it). One lock covers every operation, so reserving an attempt is
/// atomic and a sweep can never drop one another request is recording. The table is capped, and idle keys are swept away, so a
/// flood of made-up names cannot grow it without bound.</summary>
public class AttemptLimiter : IAttemptLimiter
{
    public const int AttemptsPerAccount = 5;
    public const int AttemptsPerAddress = 20;
    public const int MaxTrackedKeys = 10_000;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _attempts = new();
    private DateTimeOffset _lastSweep;

    public AttemptLimiter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _lastSweep = timeProvider.GetUtcNow();
    }

    public bool IsAddressBlocked(string? remoteAddress)
    {
        if (remoteAddress is null)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            return Count(AddressKey(remoteAddress), now) >= AttemptsPerAddress;
        }
    }

    public bool TryAcquire(string accountKey, string? remoteAddress)
    {
        var now = _timeProvider.GetUtcNow();
        var addressKey = remoteAddress is null ? null : AddressKey(remoteAddress);

        lock (_gate)
        {
            Sweep(now);

            // Check both budgets before recording either, so a refused request spends nothing.
            if (Count(accountKey, now) >= AttemptsPerAccount
                || (addressKey is not null && Count(addressKey, now) >= AttemptsPerAddress))
            {
                return false;
            }

            Record(accountKey, now);
            if (addressKey is not null)
            {
                Record(addressKey, now);
            }

            return true;
        }
    }

    public void Reset(string accountKey)
    {
        lock (_gate)
        {
            _attempts.Remove(accountKey);
        }
    }

    /// <summary>An IPv6 caller controls a whole /64, so its attempts are pooled by that prefix; an IPv4-mapped address is the
    /// plain IPv4 address.</summary>
    private static string AddressKey(string address)
    {
        if (!IPAddress.TryParse(address, out var ip))
        {
            return "ip:" + address;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = ip.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            return "ip:" + new IPAddress(bytes);
        }

        return "ip:" + ip;
    }

    private int Count(string key, DateTimeOffset now)
    {
        if (!_attempts.TryGetValue(key, out var queue))
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
        if (!_attempts.TryGetValue(key, out var queue))
        {
            if (_attempts.Count >= MaxTrackedKeys)
            {
                EvictOldest();
            }

            queue = new Queue<DateTimeOffset>();
            _attempts[key] = queue;
        }

        queue.Enqueue(now);
    }

    /// <summary>When full, the key whose latest attempt is oldest goes first.</summary>
    private void EvictOldest()
    {
        string? oldestKey = null;
        var oldest = DateTimeOffset.MaxValue;
        foreach (var (key, queue) in _attempts)
        {
            var latest = queue.Count == 0 ? DateTimeOffset.MinValue : queue.Last();
            if (latest < oldest)
            {
                oldest = latest;
                oldestKey = key;
            }
        }

        if (oldestKey is not null)
        {
            _attempts.Remove(oldestKey);
        }
    }

    private void Sweep(DateTimeOffset now)
    {
        if (now - _lastSweep < Window)
        {
            return;
        }

        _lastSweep = now;
        foreach (var key in _attempts.Keys.ToList())
        {
            if (Count(key, now) == 0)
            {
                _attempts.Remove(key);
            }
        }
    }
}
