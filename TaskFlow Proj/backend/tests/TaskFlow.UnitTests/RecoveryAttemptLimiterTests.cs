using TaskFlow.Application.Auth;
using TaskFlow.Infrastructure.Services;

namespace TaskFlow.UnitTests;

public class RecoveryAttemptLimiterTests
{
    private readonly FixedTimeProvider _clock = new(new DateTime(2026, 10, 24, 9, 0, 0));
    private readonly RecoveryAttemptLimiter _limiter;

    public RecoveryAttemptLimiterTests()
    {
        _limiter = new RecoveryAttemptLimiter(_clock);
    }

    [Fact]
    public void An_account_is_blocked_after_five_failures()
    {
        var key = IRecoveryAttemptLimiter.AccountKey(7);
        for (var i = 0; i < RecoveryAttemptLimiter.FailuresPerAccount; i++)
        {
            Assert.False(_limiter.IsBlocked(key, "10.0.0.1"));
            _limiter.RecordFailure(key, "10.0.0.1");
        }

        Assert.True(_limiter.IsBlocked(key, "10.0.0.1"));
        Assert.False(_limiter.IsBlocked(IRecoveryAttemptLimiter.AccountKey(8), "10.0.0.2"));
    }

    [Fact]
    public void An_unknown_name_is_throttled_whatever_the_case_or_spacing()
    {
        for (var i = 0; i < RecoveryAttemptLimiter.FailuresPerAccount; i++)
        {
            _limiter.RecordFailure(IRecoveryAttemptLimiter.UnknownKey(i % 2 == 0 ? "Ghost" : " GHOST "), null);
        }

        Assert.True(_limiter.IsBlocked(IRecoveryAttemptLimiter.UnknownKey("ghost"), null));
        Assert.False(_limiter.IsBlocked(IRecoveryAttemptLimiter.UnknownKey("someone-else"), null));
    }

    [Fact]
    public void The_block_lifts_after_the_window()
    {
        var key = IRecoveryAttemptLimiter.AccountKey(7);
        for (var i = 0; i < RecoveryAttemptLimiter.FailuresPerAccount; i++)
        {
            _limiter.RecordFailure(key, null);
        }

        _clock.Set(new DateTime(2026, 10, 24, 9, 6, 0));

        Assert.False(_limiter.IsBlocked(key, null));
    }

    [Fact]
    public void One_address_trying_many_accounts_is_blocked()
    {
        for (var i = 0; i < RecoveryAttemptLimiter.FailuresPerAddress; i++)
        {
            _limiter.RecordFailure(IRecoveryAttemptLimiter.AccountKey(i), "10.0.0.9");
        }

        Assert.True(_limiter.IsBlocked(IRecoveryAttemptLimiter.AccountKey(999), "10.0.0.9"));
        Assert.False(_limiter.IsBlocked(IRecoveryAttemptLimiter.AccountKey(999), "10.0.0.10"));
    }

    [Fact]
    public void A_success_clears_the_account_budget()
    {
        var key = IRecoveryAttemptLimiter.AccountKey(7);
        for (var i = 0; i < RecoveryAttemptLimiter.FailuresPerAccount - 1; i++)
        {
            _limiter.RecordFailure(key, null);
        }

        _limiter.Reset(key);
        _limiter.RecordFailure(key, null);

        Assert.False(_limiter.IsBlocked(key, null));
    }

    [Fact]
    public async Task Concurrent_failures_are_all_counted()
    {
        var key = IRecoveryAttemptLimiter.AccountKey(7);

        await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => Task.Run(() => _limiter.RecordFailure(key, null))));

        Assert.True(_limiter.IsBlocked(key, null));
    }
}
