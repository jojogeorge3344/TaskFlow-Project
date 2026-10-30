using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;

namespace TaskFlow.IntegrationTests;

/// <summary>An administrator removing someone else's MFA after a lost phone.</summary>
public class MfaAdminResetEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly MfaTestHarness _mfa;

    public MfaAdminResetEndpointsTests(TaskFlowApiFactory factory)
    {
        _mfa = new MfaTestHarness(factory);
    }

    private static string ResetUrl(int userId) => $"/api/v1/users/{userId}/mfa/reset";

    private async Task<int> UserIdAsync(string username)
    {
        using var scope = _mfa.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(u => u.Username == username).Select(u => u.Id).SingleAsync();
    }

    [Fact]
    public async Task An_administrator_reset_removes_mfa_ends_the_users_sessions_and_notifies_them()
    {
        var client = _mfa.Factory.CreateClient();
        var userToken = await _mfa.SeedAndLoginAsync(client, "mfa-reset-user");
        await _mfa.EnrolAsync(client, userToken);
        var adminToken = await _mfa.SeedAndLoginAsync(client, "mfa-reset-admin", SystemRoles.Administrator);
        var userId = await UserIdAsync("mfa-reset-user");

        var response = await MfaTestHarness.SendAsync(client, HttpMethod.Post, ResetUrl(userId), adminToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The old session is dead, so the user must sign in again.
        var oldSession = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users/me", userToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);

        // Password alone now signs in, because MFA is off.
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "mfa-reset-user", password = MfaTestHarness.Password });
        using var loginDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.True(loginDoc.RootElement.TryGetProperty("accessToken", out var token));

        var me = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users/me", token.GetString());
        using var profile = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.False(profile.RootElement.GetProperty("mfaEnabled").GetBoolean());

        var notifications = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/notifications", token.GetString());
        Assert.Contains("reset your authenticator MFA", await notifications.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_user_without_the_permission_is_refused_and_nothing_changes()
    {
        var client = _mfa.Factory.CreateClient();
        var victimToken = await _mfa.SeedAndLoginAsync(client, "mfa-reset-victim");
        await _mfa.EnrolAsync(client, victimToken);
        var employeeToken = await _mfa.SeedAndLoginAsync(client, "mfa-reset-employee");
        var victimId = await UserIdAsync("mfa-reset-victim");

        var response = await MfaTestHarness.SendAsync(client, HttpMethod.Post, ResetUrl(victimId), employeeToken);
        var anonymous = await MfaTestHarness.SendAsync(client, HttpMethod.Post, ResetUrl(victimId), token: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var me = await MfaTestHarness.SendAsync(client, HttpMethod.Get, "/api/v1/users/me", victimToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var profile = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.True(profile.RootElement.GetProperty("mfaEnabled").GetBoolean());
    }

    [Fact]
    public async Task Resetting_an_unknown_user_is_not_found()
    {
        var client = _mfa.Factory.CreateClient();
        var adminToken = await _mfa.SeedAndLoginAsync(client, "mfa-reset-admin2", SystemRoles.Administrator);

        var response = await MfaTestHarness.SendAsync(client, HttpMethod.Post, ResetUrl(999999), adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
