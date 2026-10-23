using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskFlow.IntegrationTests;

/// <summary>Changing the password needs a fresh authenticator code, and showing a typed password needs one too.</summary>
public class MfaStepUpEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string NewPassword = "N3wP@ssw0rd!";

    private readonly MfaTestHarness _mfa;

    public MfaStepUpEndpointsTests(TaskFlowApiFactory factory)
    {
        _mfa = new MfaTestHarness(factory);
    }

    private static Task<HttpResponseMessage> Change(HttpClient client, string token, object body) =>
        MfaTestHarness.SendAsync(client, HttpMethod.Post, "/api/v1/users/me/password", token, body);

    private static async Task<string> ErrorOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("error", out var error) ? error.GetString() ?? "" : "";
    }

    [Fact]
    public async Task Changing_the_password_without_a_code_is_refused_and_changes_nothing()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "step-nocode");
        await _mfa.EnrolAsync(client, token);

        var response = await Change(client, token, new { currentPassword = MfaTestHarness.Password, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("authenticator", await ErrorOf(response));
        var stillWorks = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users/me", token);
        Assert.Equal(HttpStatusCode.OK, stillWorks.StatusCode);
    }

    [Fact]
    public async Task A_wrong_code_is_refused()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "step-wrong");
        var (secret, _) = await _mfa.EnrolAsync(client, token);
        var wrong = _mfa.NextCode(secret) == "000000" ? "111111" : "000000";

        var response = await Change(client, token, new { currentPassword = MfaTestHarness.Password, newPassword = NewPassword, mfaCode = wrong });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_wrong_current_password_does_not_spend_the_code()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "step-typo");
        var (secret, _) = await _mfa.EnrolAsync(client, token);
        var code = _mfa.NextCode(secret);

        var typo = await Change(client, token, new { currentPassword = "wrong", newPassword = NewPassword, mfaCode = code });
        var retry = await Change(client, token, new { currentPassword = MfaTestHarness.Password, newPassword = NewPassword, mfaCode = code });

        Assert.Equal(HttpStatusCode.BadRequest, typo.StatusCode);
        Assert.Contains("Current password", await ErrorOf(typo));
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    [Fact]
    public async Task With_a_valid_code_the_password_changes_and_sessions_end()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "step-ok");
        var (secret, _) = await _mfa.EnrolAsync(client, token);

        var response = await Change(client, token, new { currentPassword = MfaTestHarness.Password, newPassword = NewPassword, mfaCode = _mfa.NextCode(secret) });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var old = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users/me", token);
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
    }

    [Fact]
    public async Task A_code_cannot_be_used_twice_for_password_changes()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "step-replay");
        var (secret, _) = await _mfa.EnrolAsync(client, token);
        var code = _mfa.NextCode(secret);
        var first = await Change(client, token, new { currentPassword = MfaTestHarness.Password, newPassword = NewPassword, mfaCode = code });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var relogin = await _mfa.Factory.CreateClient().PostAsync("/api/v1/auth/login",
            System.Net.Http.Json.JsonContent.Create(new { emailOrUsername = "step-replay", password = NewPassword }));
        using var document = JsonDocument.Parse(await relogin.Content.ReadAsStringAsync());
        var challenge = document.RootElement.GetProperty("challengeToken").GetString();
        var verify = await _mfa.Factory.CreateClient().PostAsync("/api/v1/auth/mfa/verify",
            System.Net.Http.Json.JsonContent.Create(new { challengeToken = challenge, code }));

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
    }

    [Fact]
    public async Task The_cosmetic_check_accepts_a_good_code_repeatedly_and_refuses_a_bad_one()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "step-check");
        var (secret, _) = await _mfa.EnrolAsync(client, token);
        var code = _mfa.NextCode(secret);

        var first = await MfaTestHarness.SendAsync(client, HttpMethod.Post, "/api/v1/users/me/mfa/check", token, new { code });
        var second = await MfaTestHarness.SendAsync(client, HttpMethod.Post, "/api/v1/users/me/mfa/check", token, new { code });
        var bad = await MfaTestHarness.SendAsync(client, HttpMethod.Post, "/api/v1/users/me/mfa/check", token, new { code = code == "000000" ? "111111" : "000000" });
        var change = await Change(client, token, new { currentPassword = MfaTestHarness.Password, newPassword = NewPassword, mfaCode = code });

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        // The same code, already used to unlock the view, still works for the real change.
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_codes_lock_the_check_with_429()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "step-lock");
        var (secret, _) = await _mfa.EnrolAsync(client, token);
        var good = _mfa.NextCode(secret);
        var wrong = good == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            await MfaTestHarness.SendAsync(client, HttpMethod.Post, "/api/v1/users/me/mfa/check", token, new { code = wrong });
        }

        var locked = await MfaTestHarness.SendAsync(client, HttpMethod.Post, "/api/v1/users/me/mfa/check", token, new { code = good });
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
    }

    [Fact]
    public async Task The_check_needs_a_signed_in_user()
    {
        var client = _mfa.Factory.CreateClient();

        var response = await MfaTestHarness.SendAsync(client, HttpMethod.Post, "/api/v1/users/me/mfa/check", token: null, new { code = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
