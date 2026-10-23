using System.Text;
using Microsoft.AspNetCore.DataProtection;
using TaskFlow.Application.Mfa;
using TaskFlow.Infrastructure.Services;

namespace TaskFlow.UnitTests;

public class Base32Tests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Encodes_the_RFC_4648_vectors_without_padding(string plain, string encoded)
    {
        Assert.Equal(encoded, Base32.Encode(Encoding.ASCII.GetBytes(plain)));
        if (plain.Length > 0)
        {
            Assert.Equal(plain, Encoding.ASCII.GetString(Base32.Decode(encoded)!));
        }
    }

    [Fact]
    public void Decode_ignores_spaces_dashes_padding_and_case_and_rejects_other_characters()
    {
        Assert.Equal("foobar", Encoding.ASCII.GetString(Base32.Decode("mzxw 6ytb-oi==")!));
        Assert.Null(Base32.Decode("MZXW1"));
        Assert.Null(Base32.Decode(""));
        Assert.Null(Base32.Decode(null));
    }

    [Fact]
    public void A_random_secret_round_trips()
    {
        var secret = Totp.NewSecret();

        Assert.Equal(Totp.SecretBytes, secret.Length);
        Assert.Equal(secret, Base32.Decode(Base32.Encode(secret)));
    }
}

public class TotpTests
{
    // RFC 6238 appendix B, SHA-1, secret "12345678901234567890". The RFC lists 8-digit codes; ours are the last 6.
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void Matches_the_RFC_6238_test_vectors(long unixSeconds, string expected)
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        Assert.Equal(expected, Totp.CodeFor(RfcSecret, Totp.StepFor(time)));
        Assert.True(Totp.TryValidate(RfcSecret, expected, time, afterStep: 0, out var step));
        Assert.Equal(Totp.StepFor(time), step);
    }

    [Fact]
    public void Accepts_the_previous_and_next_step_but_nothing_further()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var current = Totp.StepFor(now);

        Assert.True(Totp.TryValidate(RfcSecret, Totp.CodeFor(RfcSecret, current - 1), now, 0, out _));
        Assert.True(Totp.TryValidate(RfcSecret, Totp.CodeFor(RfcSecret, current + 1), now, 0, out _));
        Assert.False(Totp.TryValidate(RfcSecret, Totp.CodeFor(RfcSecret, current - 2), now, 0, out _));
        Assert.False(Totp.TryValidate(RfcSecret, Totp.CodeFor(RfcSecret, current + 2), now, 0, out _));
    }

    [Fact]
    public void A_step_already_used_or_earlier_is_refused()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var current = Totp.StepFor(now);
        var code = Totp.CodeFor(RfcSecret, current);

        Assert.True(Totp.TryValidate(RfcSecret, code, now, current - 1, out var matched));
        Assert.Equal(current, matched);
        Assert.False(Totp.TryValidate(RfcSecret, code, now, current, out _));
        Assert.False(Totp.TryValidate(RfcSecret, Totp.CodeFor(RfcSecret, current - 1), now, current, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData(" 12345")]
    public void Malformed_codes_are_rejected(string? code)
    {
        Assert.False(Totp.TryValidate(RfcSecret, code, DateTimeOffset.FromUnixTimeSeconds(59), 0, out _));
    }

    [Fact]
    public void A_wrong_secret_does_not_validate()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var code = Totp.CodeFor(RfcSecret, Totp.StepFor(now));

        Assert.False(Totp.TryValidate(Totp.NewSecret(), code, now, 0, out _));
    }

    [Fact]
    public void The_provisioning_uri_carries_the_key_issuer_and_parameters()
    {
        var uri = Totp.ProvisioningUri("TaskFlow", "jo@taskflow.com", RfcSecret);

        Assert.StartsWith("otpauth://totp/TaskFlow:jo%40taskflow.com?", uri);
        Assert.Contains($"secret={Base32.Encode(RfcSecret)}", uri);
        Assert.Contains("issuer=TaskFlow", uri);
        Assert.Contains("digits=6", uri);
        Assert.Contains("period=30", uri);
    }
}

public class MfaSecretProtectorTests
{
    private static readonly DateTime Start = new(2026, 10, 24, 9, 0, 0);

    private static MfaSecretProtector Create(IDataProtectionProvider? provider = null, FixedTimeProvider? clock = null) =>
        new(provider ?? new EphemeralDataProtectionProvider(), clock ?? new FixedTimeProvider(Start));

    [Fact]
    public void A_secret_round_trips_and_is_not_stored_in_the_clear()
    {
        var protector = Create();
        var secret = Totp.NewSecret();

        var stored = protector.Protect(secret);

        Assert.DoesNotContain(Base32.Encode(secret), stored);
        Assert.Equal(secret, protector.Unprotect(stored));
    }

    [Fact]
    public void A_tampered_missing_or_foreign_secret_returns_null()
    {
        var stored = Create().Protect(Totp.NewSecret());

        Assert.Null(Create().Unprotect(stored + "x"));
        Assert.Null(Create().Unprotect(null));
        Assert.Null(Create().Unprotect(""));
        Assert.Null(Create(new EphemeralDataProtectionProvider()).Unprotect(stored));
    }

    [Fact]
    public void A_challenge_round_trips_until_it_expires()
    {
        var clock = new FixedTimeProvider(Start);
        var provider = new EphemeralDataProtectionProvider();
        var protector = Create(provider, clock);

        var challenge = protector.CreateChallenge(42, "stamp-1", TimeSpan.FromMinutes(5));

        Assert.Equal((42, "stamp-1"), protector.ReadChallenge(challenge));

        clock.Set(Start.AddMinutes(4));
        Assert.NotNull(protector.ReadChallenge(challenge));

        clock.Set(Start.AddMinutes(5).AddSeconds(1));
        Assert.Null(protector.ReadChallenge(challenge));
    }

    [Fact]
    public void A_forged_challenge_is_rejected()
    {
        var protector = Create();
        var challenge = protector.CreateChallenge(1, "s", TimeSpan.FromMinutes(5));

        Assert.Null(protector.ReadChallenge(challenge + "A"));
        Assert.Null(protector.ReadChallenge("not-a-token"));
        Assert.Null(protector.ReadChallenge(null));
        Assert.Null(Create().ReadChallenge(challenge));
    }

    [Fact]
    public void A_secret_cannot_be_read_as_a_challenge_and_vice_versa()
    {
        var protector = Create();

        Assert.Null(protector.ReadChallenge(protector.Protect(Totp.NewSecret())));
        Assert.Null(protector.Unprotect(protector.CreateChallenge(1, "s", TimeSpan.FromMinutes(5))));
    }
}

/// <summary>The README documents these setting names; they must match the options class that binds them.</summary>
public class MfaDocumentedSettingsTests
{
    [Theory]
    [InlineData("Issuer")]
    [InlineData("MaxFailedAttempts")]
    [InlineData("LockoutMinutes")]
    [InlineData("BackupCodeCount")]
    [InlineData("ChallengeMinutes")]
    public void The_setting_exists_on_the_options_class(string name) =>
        Assert.NotNull(typeof(MfaOptions).GetProperty(name));

    [Fact]
    public void The_defaults_match_the_documentation()
    {
        var options = new MfaOptions();

        Assert.Equal("Mfa", MfaOptions.SectionName);
        Assert.Equal("TaskFlow", options.Issuer);
        Assert.Equal(5, options.MaxFailedAttempts);
        Assert.Equal(5, options.LockoutMinutes);
        Assert.Equal(10, options.BackupCodeCount);
    }
}
