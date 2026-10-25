using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskFlow.IntegrationTests;

/// <summary>Password recovery from the login page with an authenticator code.</summary>
public class MfaRecoveryEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Recover = "/api/v1/auth/password-recovery/authenticator/verify";
    private const string Reset = "/api/v1/auth/password-recovery/reset";
    private const string NewPassword = "N3wP@ssw0rd!";

    private readonly MfaTestHarness _mfa;

    public MfaRecoveryEndpointsTests(TaskFlowApiFactory factory)
    {
        _mfa = new MfaTestHarness(factory);
    }

    private static async Task<string> WithoutTraceIdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return string.Join('|', document.RootElement.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}"));
    }

    [Fact]
    public async Task A_valid_code_recovers_the_password_ends_old_sessions_and_the_new_password_works()
    {
        var client = _mfa.Factory.CreateClient();
        var oldToken = await _mfa.SeedAndLoginAsync(client, "mfa-rec-ok");
        var (secret, _) = await _mfa.EnrolAsync(client, oldToken);

        var verify = await client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-ok@taskflow.com", code = _mfa.NextCode(secret) });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        using var document = JsonDocument.Parse(await verify.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("resetToken").GetString();

        var reset = await client.PostAsJsonAsync(Reset, new { resetToken = token, newPassword = NewPassword, confirmPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var oldSession = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users/me", oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);

        var stale = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "mfa-rec-ok", password = MfaTestHarness.Password });
        var fresh = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "mfa-rec-ok", password = NewPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);

        var reuse = await client.PostAsJsonAsync(Reset, new { resetToken = token, newPassword = "An0therP@ss", confirmPassword = "An0therP@ss" });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
    }

    [Fact]
    public async Task Unknown_account_mfa_off_and_wrong_code_all_fail_the_same_way()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-rec-on");
        var (secret, _) = await _mfa.EnrolAsync(client, token);
        await _mfa.SeedAndLoginAsync(client, "mfa-rec-off");
        var wrong = _mfa.NextCode(secret) == "000000" ? "111111" : "000000";

        var unknown = await client.PostAsJsonAsync(Recover, new { identifier = "ghost@taskflow.com", code = "123456" });
        var off = await client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-off", code = "123456" });
        var badCode = await client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-on", code = wrong });

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, off.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badCode.StatusCode);
        var expected = await WithoutTraceIdAsync(unknown);
        Assert.Equal(expected, await WithoutTraceIdAsync(off));
        Assert.Equal(expected, await WithoutTraceIdAsync(badCode));
    }

    [Fact]
    public async Task A_backup_code_is_not_accepted_as_a_recovery_factor()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-rec-backup");
        var (_, backupCodes) = await _mfa.EnrolAsync(client, token);

        var response = await client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-backup", code = backupCodes[0] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_codes_in_recovery_throttle_recovery_but_never_lock_the_owner_out_of_sign_in()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-rec-lock");
        var (secret, _) = await _mfa.EnrolAsync(client, token);
        var correct = _mfa.NextCode(secret);
        var wrong = correct == "000000" ? "111111" : "000000";

        // A stranger who knows only the username keeps guessing.
        for (var i = 0; i < 5; i++)
        {
            var guess = await client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-lock", code = wrong });
            Assert.Equal(HttpStatusCode.BadRequest, guess.StatusCode);
        }

        // Recovery is now throttled, even for the right code...
        var recovery = await client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-lock", code = correct });
        Assert.Equal(HttpStatusCode.TooManyRequests, recovery.StatusCode);

        // ...but the real owner can still sign in with the password and the same code.
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "mfa-rec-lock", password = MfaTestHarness.Password });
        using var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var challenge = loginDoc.RootElement.GetProperty("challengeToken").GetString();
        var signIn = await client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { challengeToken = challenge, code = correct });

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task A_burst_of_parallel_recovery_guesses_cannot_get_past_the_budget()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-rec-burst");
        var (secret, _) = await _mfa.EnrolAsync(client, token);
        var correct = _mfa.NextCode(secret);
        var wrong = correct == "000000" ? "111111" : "000000";

        var responses = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ =>
            client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-burst", code = wrong })));

        // Only the budgeted attempts were ever checked; the rest were refused before any code was looked at.
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
        Assert.Equal(25, responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task The_username_and_the_email_of_one_account_share_a_single_recovery_budget()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-rec-share");
        var (secret, _) = await _mfa.EnrolAsync(client, token);
        var correct = _mfa.NextCode(secret);
        var wrong = correct == "000000" ? "111111" : "000000";

        // Alternating the spelling must not buy extra guesses.
        for (var i = 0; i < 5; i++)
        {
            var identifier = i % 2 == 0 ? "mfa-rec-share" : "mfa-rec-share@taskflow.com";
            await client.PostAsJsonAsync(Recover, new { identifier, code = wrong });
        }

        var byName = await client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-share", code = correct });
        var byEmail = await client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-share@taskflow.com", code = correct });

        Assert.Equal(HttpStatusCode.TooManyRequests, byName.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, byEmail.StatusCode);
    }

    [Fact]
    public async Task A_recovery_reset_notifies_the_owner()
    {
        var client = _mfa.Factory.CreateClient();
        var oldToken = await _mfa.SeedAndLoginAsync(client, "mfa-rec-notify");
        var (secret, _) = await _mfa.EnrolAsync(client, oldToken);
        var verify = await client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-notify", code = _mfa.NextCode(secret) });
        using var document = JsonDocument.Parse(await verify.Content.ReadAsStringAsync());
        var resetToken = document.RootElement.GetProperty("resetToken").GetString();

        var reset = await client.PostAsJsonAsync(Reset, new { resetToken, newPassword = NewPassword, confirmPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "mfa-rec-notify", password = NewPassword });
        using var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var challenge = loginDoc.RootElement.GetProperty("challengeToken").GetString();
        // The authenticator step was spent by the recovery, so wait for the next time step before signing in.
        var signIn = await client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { challengeToken = challenge, code = _mfa.NextCode(secret) });
        using var signInDoc = JsonDocument.Parse(await signIn.Content.ReadAsStringAsync());
        var access = signInDoc.RootElement.GetProperty("accessToken").GetString()!;

        var notifications = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/notifications", access);
        Assert.Contains("reset with your authenticator", await notifications.Content.ReadAsStringAsync());
    }
}
