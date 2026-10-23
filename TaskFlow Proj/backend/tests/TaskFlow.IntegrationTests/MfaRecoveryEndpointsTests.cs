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
    public async Task Wrong_codes_in_recovery_count_toward_the_same_lockout_as_sign_in()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-rec-lock");
        var (secret, _) = await _mfa.EnrolAsync(client, token);
        var correct = _mfa.NextCode(secret);
        var wrong = correct == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-lock", code = wrong });
        }

        // Even the right code is refused now, in recovery and at sign-in alike.
        var recovery = await client.PostAsJsonAsync(Recover, new { identifier = "mfa-rec-lock", code = correct });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "mfa-rec-lock", password = MfaTestHarness.Password });
        using var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var challenge = loginDoc.RootElement.GetProperty("challengeToken").GetString();
        var signIn = await client.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { challengeToken = challenge, code = correct });

        Assert.Equal(HttpStatusCode.BadRequest, recovery.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, signIn.StatusCode);
    }
}
