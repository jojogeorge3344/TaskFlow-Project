using TaskFlow.Api.Integrations.Security;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskFlow.IntegrationTests.Auth.Mfa;

/// <summary>Two-step sign-in over HTTP: password, then an authenticator or backup code.</summary>
public class MfaSignInEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Login = "/api/v1/auth/login";
    private const string Verify = "/api/v1/auth/mfa/verify";

    private readonly MfaTestHarness _mfa;

    public MfaSignInEndpointsTests(TaskFlowApiFactory factory)
    {
        _mfa = new MfaTestHarness(factory);
    }

    private async Task<(HttpClient Client, byte[] Secret, string[] BackupCodes)> EnrolledAsync(string username)
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, username);
        var (secret, codes) = await _mfa.EnrolAsync(client, token);
        return (_mfa.Factory.CreateClient(), secret, codes);
    }

    private static async Task<string> ChallengeAsync(HttpClient client, string username, string password = MfaTestHarness.Password)
    {
        var response = await client.PostAsJsonAsync(Login, new { emailOrUsername = username, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("challengeToken").GetString()!;
    }

    private static async Task<string> WithoutTraceIdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return string.Join('|', document.RootElement.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}"));
    }

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient client, string challenge, string code) =>
        client.PostAsJsonAsync(Verify, new { challengeToken = challenge, code });

    [Fact]
    public async Task The_password_alone_returns_a_challenge_and_no_session_or_cookie()
    {
        var (client, _, _) = await EnrolledAsync("mfa-si-challenge");

        var response = await client.PostAsJsonAsync(Login, new { emailOrUsername = "mfa-si-challenge", password = MfaTestHarness.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("mfaRequired").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("accessToken", out _));
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
    }

    [Fact]
    public async Task A_wrong_password_for_an_mfa_user_is_the_same_401_as_anywhere_else()
    {
        var (client, _, _) = await EnrolledAsync("mfa-si-wrongpw");

        var wrong = await client.PostAsJsonAsync(Login, new { emailOrUsername = "mfa-si-wrongpw", password = "nope" });
        var unknown = await client.PostAsJsonAsync(Login, new { emailOrUsername = "ghost-user", password = "nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        // Everything but the per-request trace id is identical, so MFA is not revealed.
        Assert.Equal(await WithoutTraceIdAsync(unknown), await WithoutTraceIdAsync(wrong));
    }

    [Fact]
    public async Task A_valid_code_completes_sign_in_with_a_working_session()
    {
        var (client, secret, _) = await EnrolledAsync("mfa-si-ok");
        var challenge = await ChallengeAsync(client, "mfa-si-ok");

        var response = await VerifyAsync(client, challenge, _mfa.NextCode(secret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("taskflow_refresh="));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("accessToken").GetString()!;
        var me = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users/me", token);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task A_wrong_code_is_refused_and_a_used_code_cannot_be_replayed()
    {
        var (client, secret, _) = await EnrolledAsync("mfa-si-replay");
        var challenge = await ChallengeAsync(client, "mfa-si-replay");
        var code = _mfa.NextCode(secret);
        var wrong = code == "000000" ? "111111" : "000000";

        Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(client, challenge, wrong)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(client, challenge, code)).StatusCode);

        var second = await ChallengeAsync(client, "mfa-si-replay");
        Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(client, second, code)).StatusCode);
    }

    [Fact]
    public async Task Five_wrong_codes_lock_the_account_even_against_a_correct_code_until_the_lock_ends()
    {
        var (client, secret, _) = await EnrolledAsync("mfa-si-lock");
        var challenge = await ChallengeAsync(client, "mfa-si-lock");
        var correct = _mfa.NextCode(secret);
        var wrong = correct == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(client, challenge, wrong)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await VerifyAsync(client, challenge, correct)).StatusCode);

        _mfa.Clock.Advance(5 * 60 + 5);
        var fresh = await ChallengeAsync(client, "mfa-si-lock");
        var code = Totp.CodeFor(secret, Totp.StepFor(_mfa.Clock.GetUtcNow()));
        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(client, fresh, code)).StatusCode);
    }

    [Fact]
    public async Task A_backup_code_signs_in_once()
    {
        var (client, _, backupCodes) = await EnrolledAsync("mfa-si-backup");

        var first = await VerifyAsync(client, await ChallengeAsync(client, "mfa-si-backup"), backupCodes[0]);
        var again = await VerifyAsync(client, await ChallengeAsync(client, "mfa-si-backup"), backupCodes[0]);
        var other = await VerifyAsync(client, await ChallengeAsync(client, "mfa-si-backup"), backupCodes[1].Replace("-", "").ToLowerInvariant());

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task A_forged_challenge_is_refused()
    {
        var (client, secret, _) = await EnrolledAsync("mfa-si-forged");
        var challenge = await ChallengeAsync(client, "mfa-si-forged");

        Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(client, challenge + "x", _mfa.NextCode(secret))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(client, "garbage", _mfa.NextCode(secret))).StatusCode);
    }

    [Fact]
    public async Task A_challenge_expires_after_five_minutes()
    {
        var (client, secret, _) = await EnrolledAsync("mfa-si-expire");
        var challenge = await ChallengeAsync(client, "mfa-si-expire");

        _mfa.Clock.Advance(5 * 60 + 5);
        var code = Totp.CodeFor(secret, Totp.StepFor(_mfa.Clock.GetUtcNow()));

        Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(client, challenge, code)).StatusCode);
    }

    [Fact]
    public async Task A_challenge_dies_when_the_account_security_stamp_changes()
    {
        var (client, secret, _) = await EnrolledAsync("mfa-si-stamp");
        var challenge = await ChallengeAsync(client, "mfa-si-stamp");

        // Another session changes the password, which rotates the stamp.
        var other = _mfa.Factory.CreateClient();
        var login = await VerifyAsync(other, await ChallengeAsync(other, "mfa-si-stamp"), _mfa.NextCode(secret));
        using var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginDoc.RootElement.GetProperty("accessToken").GetString()!;
        var change = await MfaTestHarness.SendAsync(other, HttpMethod.Post, "/api/v1/users/me/password", token,
            new { newPassword = "An0therP@ss", mfaCode = _mfa.NextCode(secret) });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(client, challenge, _mfa.NextCode(secret))).StatusCode);
    }
}
