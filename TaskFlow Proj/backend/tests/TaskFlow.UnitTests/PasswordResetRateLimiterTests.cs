using TaskFlow.Infrastructure.Services;

namespace TaskFlow.UnitTests;

public class PasswordResetRateLimiterTests
{
    private static readonly DateTime Start = new(2026, 10, 22, 9, 0, 0);

    [Fact]
    public void TryAcquire_allows_the_per_user_limit_then_blocks()
    {
        var clock = new FixedTimeProvider(Start);
        var limiter = new PasswordResetRateLimiter(clock);

        for (var request = 0; request < PasswordResetRateLimiter.RequestsPerUser; request++)
        {
            Assert.True(limiter.TryAcquire(userId: 7, administratorId: null));
        }

        Assert.False(limiter.TryAcquire(userId: 7, administratorId: null));
    }

    [Fact]
    public void TryAcquire_allows_again_once_the_window_has_passed()
    {
        var clock = new FixedTimeProvider(Start);
        var limiter = new PasswordResetRateLimiter(clock);

        for (var request = 0; request < PasswordResetRateLimiter.RequestsPerUser; request++)
        {
            Assert.True(limiter.TryAcquire(userId: 7, administratorId: null));
        }

        Assert.False(limiter.TryAcquire(userId: 7, administratorId: null));

        clock.Set(Start.AddMinutes(5));

        Assert.True(limiter.TryAcquire(userId: 7, administratorId: null));
    }

    [Fact]
    public void TryAcquire_keeps_a_separate_bucket_per_user()
    {
        var limiter = new PasswordResetRateLimiter(new FixedTimeProvider(Start));

        for (var request = 0; request < PasswordResetRateLimiter.RequestsPerUser; request++)
        {
            Assert.True(limiter.TryAcquire(userId: 1, administratorId: null));
        }

        Assert.False(limiter.TryAcquire(userId: 1, administratorId: null));
        Assert.True(limiter.TryAcquire(userId: 2, administratorId: null));
    }

    [Fact]
    public void TryAcquire_throttles_the_administrator_mailbox_across_requesting_users()
    {
        var limiter = new PasswordResetRateLimiter(new FixedTimeProvider(Start));

        for (var user = 1; user <= PasswordResetRateLimiter.RequestsPerAdministrator; user++)
        {
            Assert.True(limiter.TryAcquire(userId: user, administratorId: 99));
        }

        // A brand-new requesting account is still refused because the administrator's inbox is full.
        Assert.False(limiter.TryAcquire(userId: 1000, administratorId: 99));

        // A different administrator has their own budget.
        Assert.True(limiter.TryAcquire(userId: 1001, administratorId: 100));
    }
}
