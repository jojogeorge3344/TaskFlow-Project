using System.Net;
using System.Text.Json;

namespace TaskFlow.IntegrationTests.Auth.Mfa;

/// <summary>The Profile side of authenticator MFA: setup, confirm, backup codes and turning it off.</summary>
public class MfaEnrolmentEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Mfa = "/api/v1/users/me/mfa";

    private readonly MfaTestHarness _mfa;

    public MfaEnrolmentEndpointsTests(TaskFlowApiFactory factory)
    {
        _mfa = new MfaTestHarness(factory);
    }

    private HttpClient NewClient() => _mfa.Factory.CreateClient();

    private static Task<HttpResponseMessage> Post(HttpClient client, string path, string? token, object body) =>
        MfaTestHarness.SendAsync(client, HttpMethod.Post, $"{Mfa}/{path}", token, body);

    [Fact]
    public async Task Every_mfa_endpoint_needs_a_signed_in_user()
    {
        var client = NewClient();

        foreach (var path in new[] { "setup", "enable", "check", "backup-codes" })
        {
            var response = await Post(client, path, token: null, new { currentPassword = "x", code = "123456" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Setup_with_the_wrong_password_issues_no_key()
    {
        var client = NewClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-ep-wrongpw");

        var response = await Post(client, "setup", token, new { currentPassword = "wrong" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("otpauth", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Setup_returns_the_key_and_qr_uri_but_does_not_turn_mfa_on()
    {
        var client = NewClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-ep-setup");

        var response = await Post(client, "setup", token, new { currentPassword = MfaTestHarness.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("key").GetString()));
        Assert.StartsWith("otpauth://totp/TaskFlow:", document.RootElement.GetProperty("provisioningUri").GetString());

        var me = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users/me", token);
        using var profile = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.False(profile.RootElement.GetProperty("mfaEnabled").GetBoolean());
    }

    [Fact]
    public async Task Enabling_with_a_wrong_code_fails_and_with_the_right_code_returns_ten_backup_codes()
    {
        var client = NewClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-ep-enable");
        var setup = await Post(client, "setup", token, new { currentPassword = MfaTestHarness.Password });
        using var setupDoc = JsonDocument.Parse(await setup.Content.ReadAsStringAsync());
        var secret = TaskFlow.Api.Integrations.Security.Base32.Decode(setupDoc.RootElement.GetProperty("key").GetString())!;

        var wrong = await Post(client, "enable", token, new { code = "000000" });
        var right = await Post(client, "enable", token, new { code = _mfa.NextCode(secret) });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        using var document = JsonDocument.Parse(await right.Content.ReadAsStringAsync());
        Assert.Equal(10, document.RootElement.GetProperty("codes").GetArrayLength());
    }

    [Fact]
    public async Task After_enabling_the_profile_shows_status_and_remaining_codes_but_never_the_key_or_codes()
    {
        var client = NewClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-ep-status");
        var (secret, codes) = await _mfa.EnrolAsync(client, token);

        var me = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users/me", token);
        var body = await me.Content.ReadAsStringAsync();

        using var profile = JsonDocument.Parse(body);
        Assert.True(profile.RootElement.GetProperty("mfaEnabled").GetBoolean());
        Assert.Equal(10, profile.RootElement.GetProperty("backupCodesRemaining").GetInt32());
        Assert.DoesNotContain(TaskFlow.Api.Integrations.Security.Base32.Encode(secret), body);
        Assert.All(codes, code => Assert.DoesNotContain(code, body));
    }

    [Fact]
    public async Task Setup_is_refused_when_mfa_is_already_on()
    {
        var client = NewClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-ep-twice");
        await _mfa.EnrolAsync(client, token);

        var response = await Post(client, "setup", token, new { currentPassword = MfaTestHarness.Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Regenerating_backup_codes_replaces_the_set()
    {
        var client = NewClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-ep-regen");
        var (secret, oldCodes) = await _mfa.EnrolAsync(client, token);

        var wrong = await Post(client, "backup-codes", token, new { currentPassword = MfaTestHarness.Password, code = "000000" });
        var right = await Post(client, "backup-codes", token, new { currentPassword = MfaTestHarness.Password, code = _mfa.NextCode(secret) });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        using var document = JsonDocument.Parse(await right.Content.ReadAsStringAsync());
        var fresh = document.RootElement.GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        Assert.Equal(10, fresh.Length);
        Assert.Empty(fresh.Intersect(oldCodes));
    }

    [Fact]
    public async Task There_is_no_way_to_turn_mfa_off()
    {
        var client = NewClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mfa-ep-nodisable");
        var (secret, _) = await _mfa.EnrolAsync(client, token);

        var response = await Post(client, "disable", token, new { currentPassword = MfaTestHarness.Password, code = _mfa.NextCode(secret) });

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        var me = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users/me", token);
        using var profile = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.True(profile.RootElement.GetProperty("mfaEnabled").GetBoolean());
    }
}
