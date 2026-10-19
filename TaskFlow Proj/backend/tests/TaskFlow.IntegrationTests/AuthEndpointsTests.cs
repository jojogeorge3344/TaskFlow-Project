using System.Net;
using TaskFlow.Domain.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

public class AuthEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public AuthEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsUserRoleAndSecureCookie()
    {
        await _factory.SeedUserAsync("admin", "admin@taskflow.local", "P@ssw0rd!", SystemRoles.Administrator);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = "admin@taskflow.local",
            password = "P@ssw0rd!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponseBody>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal("Administrator", body.User.Role);

        var setCookie = string.Join(";", response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("taskflow_refresh=", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsGenericUnauthorized()
    {
        await _factory.SeedUserAsync("employee1", "employee1@taskflow.local", "P@ssw0rd!", SystemRoles.Employee);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = "employee1",
            password = "not-the-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inactive", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WhenInactive_IsIndistinguishableFromUnknownUser()
    {
        await _factory.SeedUserAsync("disabled", "disabled@taskflow.local", "P@ssw0rd!", SystemRoles.Employee, isActive: false);
        var client = _factory.CreateClient();

        var inactive = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = "disabled@taskflow.local",
            password = "P@ssw0rd!"
        });

        var unknown = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = "ghost@taskflow.local",
            password = "P@ssw0rd!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);

        var inactiveBody = await inactive.Content.ReadAsStringAsync();
        var unknownBody = await unknown.Content.ReadAsStringAsync();

        Assert.DoesNotContain("inactive", inactiveBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("disabled", inactiveBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unknown", inactiveBody, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(StripTraceId(unknownBody), StripTraceId(inactiveBody));
    }

    [Fact]
    public async Task Login_ResponseBody_NeverLeaksPasswordMaterial()
    {
        await _factory.SeedUserAsync("manager1", "manager1@taskflow.local", "P@ssw0rd!", SystemRoles.Manager);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = "manager1",
            password = "P@ssw0rd!"
        });

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("P@ssw0rd!", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_RotatesCookie_AndRejectsReplay()
    {
        await _factory.SeedUserAsync("employee2", "employee2@taskflow.local", "P@ssw0rd!", SystemRoles.Employee);
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = "employee2",
            password = "P@ssw0rd!"
        });

        var originalCookie = ExtractRefreshCookie(login);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"taskflow_refresh={originalCookie}");

        var refresh = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotatedCookie = ExtractRefreshCookie(refresh);
        Assert.NotEqual(originalCookie, rotatedCookie);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        replayRequest.Headers.Add("Cookie", $"taskflow_refresh={originalCookie}");
        var replay = await client.SendAsync(replayRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Me_WithAccessToken_ReturnsCurrentUser()
    {
        await _factory.SeedUserAsync("admin2", "admin2@taskflow.local", "P@ssw0rd!", SystemRoles.Administrator);
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = "admin2",
            password = "P@ssw0rd!"
        });

        var body = await login.Content.ReadFromJsonAsync<AuthResponseBody>();

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);

        var me = await client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await me.Content.ReadFromJsonAsync<UserBody>();
        Assert.NotNull(meBody);
        Assert.Equal("admin2", meBody!.Username);
        Assert.Equal("Administrator", meBody.Role);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string StripTraceId(string body) =>
        System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"normalized\"");

    private static string ExtractRefreshCookie(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("taskflow_refresh=", StringComparison.OrdinalIgnoreCase));

        var pair = header.Split(';')[0];
        return pair[(pair.IndexOf('=') + 1)..];
    }

    private sealed record UserBody(
        int Id,
        string FullName,
        string Username,
        string Email,
        string Role,
        string? EmployeeCode);

    private sealed record AuthResponseBody(string AccessToken, DateTime ExpiresAt, UserBody User);
}
