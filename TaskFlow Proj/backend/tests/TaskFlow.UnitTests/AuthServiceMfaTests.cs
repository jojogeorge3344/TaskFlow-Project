using TaskFlow.Api.Dtos.Auth.Mfa;
using TaskFlow.Api.Services.Interfaces.Auth.Mfa;
using TaskFlow.Api.Dtos.Auth;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.UnitTests;

/// <summary>Sign-in when the account has MFA on: the password alone must never produce a session.</summary>
public class AuthServiceMfaTests
{
    private const string Password = "P@ssw0rd!";

    [Fact]
    public async Task A_correct_password_with_mfa_on_returns_a_challenge_and_no_session()
    {
        await using var harness = AuthServiceHarness.Create();
        var user = harness.AddUser("mfa-login", "mfa-login@taskflow.com", Password);
        user.MfaEnabledAt = DateTime.UtcNow;
        await harness.Db.SaveChangesAsync();

        var result = await harness.Service.LoginAsync(new LoginRequest("mfa-login", Password));

        Assert.True(result.MfaRequired);
        Assert.False(result.Succeeded);
        Assert.Null(result.Tokens);
        Assert.Null(result.User);
        Assert.False(string.IsNullOrWhiteSpace(result.MfaChallengeToken));
        Assert.Empty(await harness.Db.RefreshTokens.ToListAsync());
        Assert.Null((await harness.Db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).LastLoginAt);
    }

    [Fact]
    public async Task A_wrong_password_with_mfa_on_fails_exactly_like_any_other_account()
    {
        await using var harness = AuthServiceHarness.Create();
        var withMfa = harness.AddUser("mfa-wrong", "mfa-wrong@taskflow.com", Password);
        withMfa.MfaEnabledAt = DateTime.UtcNow;
        harness.AddUser("plain-wrong", "plain-wrong@taskflow.com", Password);
        await harness.Db.SaveChangesAsync();

        var mfaResult = await harness.Service.LoginAsync(new LoginRequest("mfa-wrong", "nope"));
        var plainResult = await harness.Service.LoginAsync(new LoginRequest("plain-wrong", "nope"));

        Assert.Equal(plainResult, mfaResult);
        Assert.False(mfaResult.MfaRequired);
        Assert.False(mfaResult.Succeeded);
    }

    [Fact]
    public async Task An_account_without_mfa_still_signs_in_with_the_password_alone()
    {
        await using var harness = AuthServiceHarness.Create();
        harness.AddUser("no-mfa", "no-mfa@taskflow.com", Password);

        var result = await harness.Service.LoginAsync(new LoginRequest("no-mfa", Password));

        Assert.True(result.Succeeded);
        Assert.False(result.MfaRequired);
    }

    [Fact]
    public async Task A_forged_or_empty_challenge_never_signs_anyone_in()
    {
        await using var harness = AuthServiceHarness.Create();

        foreach (var challenge in new[] { null, "", "garbage", "CfDJ8-not-a-real-token" })
        {
            var result = await harness.Service.VerifyMfaAsync(challenge, "123456");

            Assert.False(result.Auth.Succeeded);
            Assert.Equal(MfaVerifyOutcome.Invalid, result.Outcome);
        }
    }
}
