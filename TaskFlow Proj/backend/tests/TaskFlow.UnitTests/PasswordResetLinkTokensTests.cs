using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using TaskFlow.Infrastructure.Services;

namespace TaskFlow.UnitTests;

public class PasswordResetLinkTokensTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static PasswordResetLinkTokens Create(ManualClock clock, int minutes = 5, IDataProtectionProvider? provider = null) =>
        new(provider ?? new EphemeralDataProtectionProvider(), Options.Create(new PasswordResetOptions { LinkLifetimeMinutes = minutes }), clock);

    [Fact]
    public void ATokenRoundTripsTheUserAndTheRequest()
    {
        var tokens = Create(new ManualClock());

        var token = tokens.Create(42, 7);

        Assert.Equal((42, 7), tokens.Read(token));
    }

    [Fact]
    public void ATokenIsUrlSafe_AndShowsNeitherTheIdsNorAnythingReadable()
    {
        var token = Create(new ManualClock()).Create(42, 7);

        Assert.Matches("^[A-Za-z0-9_-]+$", token);
        Assert.DoesNotContain("@", token, StringComparison.Ordinal);
    }

    [Fact]
    public void ATokenExpiresFiveMinutesAfterItIsMade_ButNotBefore()
    {
        var clock = new ManualClock();
        var tokens = Create(clock);
        var token = tokens.Create(1, 1);

        clock.Now = clock.Now.AddMinutes(5).AddSeconds(-1);
        Assert.NotNull(tokens.Read(token));

        clock.Now = clock.Now.AddSeconds(2);
        Assert.Null(tokens.Read(token));
    }

    [Fact]
    public void TheDefaultLifetimeIsFiveMinutes_AndTheConfiguredOneIsUsed()
    {
        var clock = new ManualClock();
        Assert.Equal(1, new PasswordResetOptions().LinkLifetimeMinutes);

        var tokens = Create(clock, minutes: 1);
        var token = tokens.Create(1, 1);
        clock.Now = clock.Now.AddSeconds(61);

        Assert.Null(tokens.Read(token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-token")]
    [InlineData("AAAA")]
    public void MalformedInputReadsAsNull(string? token)
    {
        Assert.Null(Create(new ManualClock()).Read(token));
    }

    [Fact]
    public void ATamperedTokenReadsAsNull()
    {
        var tokens = Create(new ManualClock());
        var token = tokens.Create(1, 1);

        var tampered = token[..^4] + (token[^4] == 'A' ? 'B' : 'A') + token[^3..];

        Assert.Null(tokens.Read(tampered));
    }

    [Fact]
    public void ATokenFromAnotherKeyRingReadsAsNull()
    {
        var clock = new ManualClock();
        var token = Create(clock, provider: new EphemeralDataProtectionProvider()).Create(1, 1);

        Assert.Null(Create(clock, provider: new EphemeralDataProtectionProvider()).Read(token));
    }
}
