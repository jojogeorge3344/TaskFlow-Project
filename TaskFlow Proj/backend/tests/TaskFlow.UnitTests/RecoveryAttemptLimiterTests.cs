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
    public void An_account_is_blocked_after_five_failures_whatever_the_case_of_the_name()
    {
        for (var i = 0; i < RecoveryAttemptLimiter.FailuresPerIdentifier; i++)
        {
            Assert.False(_limiter.IsBlocked("Alice", "10.0.0.1"));
            _limiter.RecordFailure(i % 2 == 0 ? "Alice" : "ALICE ", "10.0.0.1");
        }

        Assert.True(_limiter.IsBlocked("alice", "10.0.0.1"));
        Assert.False(_limiter.IsBlocked("bob", "10.0.0.2"));
    }

    [Fact]
    public void The_block_lifts_after_the_window()
    {
        for (var i = 0; i < RecoveryAttemptLimiter.FailuresPerIdentifier; i++)
        {
            _limiter.RecordFailure("alice", null);
        }

        _clock.Set(new DateTime(2026, 10, 24, 9, 6, 0));

        Assert.False(_limiter.IsBlocked("alice", null));
    }

    [Fact]
    public void One_address_trying_many_accounts_is_blocked()
    {
        for (var i = 0; i < RecoveryAttemptLimiter.FailuresPerAddress; i++)
        {
            _limiter.RecordFailure("user" + i, "10.0.0.9");
        }

        Assert.True(_limiter.IsBlocked("someone-new", "10.0.0.9"));
        Assert.False(_limiter.IsBlocked("someone-new", "10.0.0.10"));
    }

    [Fact]
    public void A_success_clears_the_account_budget()
    {
        for (var i = 0; i < RecoveryAttemptLimiter.FailuresPerIdentifier - 1; i++)
        {
            _limiter.RecordFailure("alice", null);
        }

        _limiter.Reset("alice");
        _limiter.RecordFailure("alice", null);

        Assert.False(_limiter.IsBlocked("alice", null));
    }
}
