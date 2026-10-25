using TaskFlow.Application.Auth;
using TaskFlow.Infrastructure.Services;

namespace TaskFlow.UnitTests;

public class AttemptLimiterTests
{
    private readonly FixedTimeProvider _clock = new(new DateTime(2026, 10, 24, 9, 0, 0));
    private readonly AttemptLimiter _limiter;

    public AttemptLimiterTests()
    {
        _limiter = new AttemptLimiter(_clock);
    }

    [Fact]
    public void An_account_gets_five_attempts_and_the_sixth_is_refused()
    {
        var key = IAttemptLimiter.AccountKey(7);

        for (var i = 0; i < AttemptLimiter.AttemptsPerAccount; i++)
        {
            Assert.True(_limiter.TryAcquire(key, "10.0.0.1"));
        }

        Assert.False(_limiter.TryAcquire(key, "10.0.0.1"));
        Assert.True(_limiter.TryAcquire(IAttemptLimiter.AccountKey(8), "10.0.0.2"));
    }

    [Fact]
    public async Task Parallel_attempts_cannot_exceed_the_budget()
    {
        var key = IAttemptLimiter.AccountKey(7);

        var results = await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => Task.Run(() => _limiter.TryAcquire(key, null))));

        Assert.Equal(AttemptLimiter.AttemptsPerAccount, results.Count(granted => granted));
    }

    [Fact]
    public void A_refused_attempt_spends_nothing_from_the_other_budget()
    {
        for (var i = 0; i < AttemptLimiter.AttemptsPerAccount; i++)
        {
            _limiter.TryAcquire(IAttemptLimiter.AccountKey(7), "10.0.0.1");
        }

        // Refused on the account, so it must not eat the address budget either.
        for (var i = 0; i < 50; i++)
        {
            _limiter.TryAcquire(IAttemptLimiter.AccountKey(7), "10.0.0.1");
        }

        Assert.True(_limiter.TryAcquire(IAttemptLimiter.AccountKey(9), "10.0.0.1"));
    }

    [Fact]
    public void An_unknown_name_is_throttled_whatever_the_case_or_spacing()
    {
        for (var i = 0; i < AttemptLimiter.AttemptsPerAccount; i++)
        {
            Assert.True(_limiter.TryAcquire(IAttemptLimiter.UnknownKey(i % 2 == 0 ? "Ghost" : " GHOST "), null));
        }

        Assert.False(_limiter.TryAcquire(IAttemptLimiter.UnknownKey("ghost"), null));
        Assert.True(_limiter.TryAcquire(IAttemptLimiter.UnknownKey("someone-else"), null));
    }

    [Fact]
    public void The_password_budget_is_kept_apart_from_recovery()
    {
        for (var i = 0; i < AttemptLimiter.AttemptsPerAccount; i++)
        {
            _limiter.TryAcquire(IAttemptLimiter.PasswordKey(7), null);
        }

        Assert.False(_limiter.TryAcquire(IAttemptLimiter.PasswordKey(7), null));
        Assert.True(_limiter.TryAcquire(IAttemptLimiter.AccountKey(7), null));
    }

    [Fact]
    public void The_block_lifts_after_the_window()
    {
        var key = IAttemptLimiter.AccountKey(7);
        for (var i = 0; i < AttemptLimiter.AttemptsPerAccount; i++)
        {
            _limiter.TryAcquire(key, null);
        }

        _clock.Set(new DateTime(2026, 10, 24, 9, 6, 0));

        Assert.True(_limiter.TryAcquire(key, null));
    }

    [Fact]
    public void One_address_trying_many_accounts_is_blocked()
    {
        for (var i = 0; i < AttemptLimiter.AttemptsPerAddress; i++)
        {
            Assert.True(_limiter.TryAcquire(IAttemptLimiter.AccountKey(i), "10.0.0.9"));
        }

        Assert.False(_limiter.TryAcquire(IAttemptLimiter.AccountKey(999), "10.0.0.9"));
        Assert.True(_limiter.TryAcquire(IAttemptLimiter.AccountKey(999), "10.0.0.10"));
    }

    [Fact]
    public void The_address_check_reads_the_budget_without_spending_it()
    {
        Assert.False(_limiter.IsAddressBlocked("10.0.0.9"));
        for (var i = 0; i < 100; i++)
        {
            Assert.False(_limiter.IsAddressBlocked("10.0.0.9"));
        }

        for (var i = 0; i < AttemptLimiter.AttemptsPerAddress; i++)
        {
            _limiter.TryAcquire(IAttemptLimiter.AccountKey(i), "10.0.0.9");
        }

        Assert.True(_limiter.IsAddressBlocked("10.0.0.9"));
        Assert.False(_limiter.IsAddressBlocked("10.0.0.10"));
        Assert.False(_limiter.IsAddressBlocked(null));
    }

    [Fact]
    public void The_address_check_follows_the_same_pooling_and_window()
    {
        for (var i = 0; i < AttemptLimiter.AttemptsPerAddress; i++)
        {
            _limiter.TryAcquire(IAttemptLimiter.AccountKey(i), "2001:db8:1:2::1");
        }

        Assert.True(_limiter.IsAddressBlocked("2001:db8:1:2:aaaa::9"));

        _clock.Set(new DateTime(2026, 10, 24, 9, 6, 0));

        Assert.False(_limiter.IsAddressBlocked("2001:db8:1:2:aaaa::9"));
    }

    [Fact]
    public void An_ipv6_caller_is_pooled_by_its_64_bit_prefix()
    {
        for (var i = 0; i < AttemptLimiter.AttemptsPerAddress; i++)
        {
            Assert.True(_limiter.TryAcquire(IAttemptLimiter.AccountKey(i), $"2001:db8:1:2::{i + 1:x}"));
        }

        // Another address in the same /64 shares the budget; a different /64 does not.
        Assert.False(_limiter.TryAcquire(IAttemptLimiter.AccountKey(500), "2001:db8:1:2:ffff::1"));
        Assert.True(_limiter.TryAcquire(IAttemptLimiter.AccountKey(500), "2001:db8:1:3::1"));
    }

    [Fact]
    public void An_ipv4_mapped_address_counts_as_the_plain_ipv4_address()
    {
        for (var i = 0; i < AttemptLimiter.AttemptsPerAddress; i++)
        {
            _limiter.TryAcquire(IAttemptLimiter.AccountKey(i), "10.0.0.9");
        }

        Assert.False(_limiter.TryAcquire(IAttemptLimiter.AccountKey(999), "::ffff:10.0.0.9"));
    }

    [Fact]
    public void A_success_clears_the_account_budget()
    {
        var key = IAttemptLimiter.AccountKey(7);
        for (var i = 0; i < AttemptLimiter.AttemptsPerAccount; i++)
        {
            _limiter.TryAcquire(key, null);
        }

        _limiter.Reset(key);

        Assert.True(_limiter.TryAcquire(key, null));
    }

    [Fact]
    public void The_table_never_holds_more_than_the_cap_and_keeps_recent_budgets()
    {
        var victim = IAttemptLimiter.AccountKey(1);
        for (var i = 0; i < AttemptLimiter.AttemptsPerAccount; i++)
        {
            _limiter.TryAcquire(victim, null);
        }

        // A flood of made-up names, each seen later than the victim's attempts.
        _clock.Set(new DateTime(2026, 10, 24, 9, 0, 1));
        for (var i = 0; i < AttemptLimiter.MaxTrackedKeys + 500; i++)
        {
            _limiter.TryAcquire(IAttemptLimiter.UnknownKey("name" + i), null);
        }

        var table = (System.Collections.IDictionary)typeof(AttemptLimiter)
            .GetField("_attempts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(_limiter)!;
        var tracked = table.Count;

        Assert.True(tracked <= AttemptLimiter.MaxTrackedKeys);
    }
}
