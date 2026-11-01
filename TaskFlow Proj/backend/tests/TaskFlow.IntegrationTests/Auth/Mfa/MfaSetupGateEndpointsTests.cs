using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.IntegrationTests.Auth.Mfa;

/// <summary>With MFA required, a user who has not set up an authenticator app can reach only the setup endpoints, and the
/// whole app opens the moment they finish.</summary>
public class MfaSetupGateEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly MfaTestHarness _mfa;

    public MfaSetupGateEndpointsTests(TaskFlowApiFactory factory)
    {
        _mfa = new MfaTestHarness(factory, requireMfa: true);
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task Sign_in_tells_the_client_that_setup_is_required()
    {
        var client = _mfa.Factory.CreateClient();
        await _mfa.SeedAndLoginAsync(client, "gate-login");

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "gate-login", password = MfaTestHarness.Password });

        using var document = await ReadAsync(login);
        Assert.True(document.RootElement.GetProperty("user").GetProperty("mfaSetupRequired").GetBoolean());
    }

    [Theory]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/tasks")]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/v1/dashboard/employee")]
    public async Task Everything_else_is_refused_until_setup_is_finished(string url)
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, $"gate-blocked-{Math.Abs(url.GetHashCode())}");

        var response = await MfaTestHarness.SendAsync(client, HttpMethod.Get, url, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = await ReadAsync(response);
        Assert.Equal("mfa_setup_required", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_setup_screen_can_still_read_and_save_the_profile_and_start_setup()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "gate-allowed");

        var me = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users/me", token);
        var save = await MfaTestHarness.SendAsync(client, HttpMethod.Put, "/api/v1/users/me", token,
            new { fullName = "Gate Allowed", email = "gate-allowed@taskflow.com", mobileNumber = "+919876543210" });
        var setup = await MfaTestHarness.SendAsync(client, HttpMethod.Post, "/api/v1/users/me/mfa/setup", token,
            new { currentPassword = MfaTestHarness.Password });
        var session = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/auth/me", token);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    [Fact]
    public async Task Other_profile_actions_stay_blocked_during_setup()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "gate-profile");

        var password = await MfaTestHarness.SendAsync(client, HttpMethod.Post, "/api/v1/users/me/password", token,
            new { newPassword = "An0therP@ss" });

        Assert.Equal(HttpStatusCode.Forbidden, password.StatusCode);
    }

    [Fact]
    public async Task Finishing_setup_opens_the_app_straight_away_with_the_same_session()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "gate-open");
        Assert.Equal(HttpStatusCode.Forbidden, (await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/notifications", token)).StatusCode);

        await _mfa.EnrolAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, (await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/notifications", token)).StatusCode);
        var session = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/auth/me", token);
        using var document = await ReadAsync(session);
        Assert.False(document.RootElement.GetProperty("mfaSetupRequired").GetBoolean());
    }

    [Fact]
    public async Task An_administrator_reset_puts_the_gate_back_up()
    {
        var client = _mfa.Factory.CreateClient();
        var userToken = await _mfa.SeedAndLoginAsync(client, "gate-reset-user");
        await _mfa.EnrolAsync(client, userToken);
        var adminToken = await _mfa.SeedAndLoginAsync(client, "gate-reset-admin", SystemRoles.Administrator);
        await _mfa.EnrolAsync(client, adminToken);

        using var users = await ReadAsync(await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users?search=gate-reset-user", adminToken));
        var userId = users.RootElement.EnumerateArray().Single(u => u.GetProperty("username").GetString() == "gate-reset-user").GetProperty("id").GetInt32();
        await MfaTestHarness.SendAsync(client, HttpMethod.Post, $"/api/v1/users/{userId}/mfa/reset", adminToken);

        var relogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "gate-reset-user", password = MfaTestHarness.Password });
        using var document = await ReadAsync(relogin);
        Assert.True(document.RootElement.GetProperty("user").GetProperty("mfaSetupRequired").GetBoolean());
        var token = document.RootElement.GetProperty("accessToken").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/notifications", token)).StatusCode);
    }

    [Fact]
    public async Task The_login_endpoints_and_recovery_are_never_gated()
    {
        var client = _mfa.Factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "nobody", password = "x" });
        var recover = await client.PostAsJsonAsync("/api/v1/auth/password-recovery/authenticator/verify", new { identifier = "nobody", code = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, recover.StatusCode);
    }
}

/// <summary>The optional contact mobile number on the profile. Email is the only unique identity, so colleagues may share a number.</summary>
public class ProfileMobileNumberEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly MfaTestHarness _mfa;

    public ProfileMobileNumberEndpointsTests(TaskFlowApiFactory factory)
    {
        _mfa = new MfaTestHarness(factory);
    }

    private static Task<HttpResponseMessage> Save(HttpClient client, string token, string username, object? mobile) =>
        MfaTestHarness.SendAsync(client, HttpMethod.Put, "/api/v1/users/me", token,
            mobile is null
                ? new { fullName = username, email = $"{username}@taskflow.com" }
                : (object)new { fullName = username, email = $"{username}@taskflow.com", mobileNumber = mobile });

    private static async Task<string?> MobileOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("mobileNumber", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    [Fact]
    public async Task A_valid_number_is_saved_cleaned_up_and_returned()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mob-save");

        var response = await Save(client, token, "mob-save", "+91 98765-43210");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("+919876543210", await MobileOf(response));
    }

    [Fact]
    public async Task A_badly_formatted_number_is_refused()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mob-bad");

        Assert.Equal(HttpStatusCode.BadRequest, (await Save(client, token, "mob-bad", "98765")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(client, token, "mob-bad", "+0123456789")).StatusCode);
    }

    [Fact]
    public async Task Colleagues_with_different_emails_can_share_one_number()
    {
        var client = _mfa.Factory.CreateClient();
        var first = await _mfa.SeedAndLoginAsync(client, "mob-share-one");
        var second = await _mfa.SeedAndLoginAsync(client, "mob-share-two");

        var one = await Save(client, first, "mob-share-one", "+919526624166");
        var two = await Save(client, second, "mob-share-two", "+919526624166");

        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.Equal(HttpStatusCode.OK, two.StatusCode);
        Assert.Equal("+919526624166", await MobileOf(two));
    }

    [Fact]
    public async Task Email_is_still_unique_even_when_the_number_matches()
    {
        var client = _mfa.Factory.CreateClient();
        var first = await _mfa.SeedAndLoginAsync(client, "mob-email-one");
        var second = await _mfa.SeedAndLoginAsync(client, "mob-email-two");
        await Save(client, first, "mob-email-one", "+919111111111");

        var response = await MfaTestHarness.SendAsync(client, HttpMethod.Put, "/api/v1/users/me", second,
            new { fullName = "Two", email = "mob-email-one@taskflow.com", mobileNumber = "+919111111111" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Leaving_the_number_out_keeps_it_and_an_empty_value_clears_it()
    {
        var client = _mfa.Factory.CreateClient();
        var token = await _mfa.SeedAndLoginAsync(client, "mob-keep");
        await Save(client, token, "mob-keep", "+919222222222");

        var kept = await Save(client, token, "mob-keep", null);
        var cleared = await Save(client, token, "mob-keep", "");

        Assert.Equal("+919222222222", await MobileOf(kept));
        Assert.Null(await MobileOf(cleared));
    }
}
