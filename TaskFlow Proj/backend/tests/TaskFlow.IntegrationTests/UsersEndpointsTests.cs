using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

public class UsersEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public UsersEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_WithRoleName_ReturnsRoleName()
    {
        var client = _factory.CreateClient();
        var token = (await LoginAsync(client, "users-admin-create", Role.Administrator)).AccessToken;

        var response = await SendAsync(client, HttpMethod.Post, "/api/v1/users", token, new
        {
            employeeCode = "EMP-900",
            fullName = "Created Through Api",
            username = "created.api",
            email = "created.api@taskflow.local",
            password = "Temp#12345",
            role = "Manager",
            isActive = true,
            departmentId = (int?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Manager", document.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task GetAll_ReturnsRolesAsNames()
    {
        var client = _factory.CreateClient();
        var token = (await LoginAsync(client, "users-admin-list", Role.Administrator)).AccessToken;

        var response = await SendAsync(client, HttpMethod.Get, "/api/v1/users?role=Administrator", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var roles = document.RootElement.EnumerateArray().Select(user => user.GetProperty("role")).ToList();
        Assert.NotEmpty(roles);
        Assert.All(roles, role => Assert.Equal("Administrator", role.GetString()));
    }

    [Fact]
    public async Task Update_WithNewPassword_EndsExistingSessions_AndOnlyNewPasswordWorks()
    {
        var adminClient = _factory.CreateClient();
        var adminToken = (await LoginAsync(adminClient, "users-admin-pwd", Role.Administrator)).AccessToken;
        var employeeClient = _factory.CreateClient();
        var employee = await LoginAsync(employeeClient, "users-emp-pwd", Role.Employee);

        var update = await SendAsync(adminClient, HttpMethod.Put, $"/api/v1/users/{employee.UserId}", adminToken,
            UpdateBody("users-emp-pwd", password: "NewPass#1"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        // The access token issued before the change is rejected immediately.
        var me = await SendAsync(employeeClient, HttpMethod.Get, "/api/v1/auth/me", employee.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        // The refresh token was revoked, so the session cannot be renewed.
        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refresh.Headers.Add("Cookie", $"taskflow_refresh={employee.RefreshCookie}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await employeeClient.SendAsync(refresh)).StatusCode);

        var oldLogin = await PostLoginAsync(employeeClient, "users-emp-pwd", Password);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await PostLoginAsync(employeeClient, "users-emp-pwd", "NewPass#1");
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Update_WithNewUsername_EndsExistingSessions()
    {
        var adminClient = _factory.CreateClient();
        var adminToken = (await LoginAsync(adminClient, "users-admin-uname", Role.Administrator)).AccessToken;
        var employeeClient = _factory.CreateClient();
        var employee = await LoginAsync(employeeClient, "users-emp-uname", Role.Employee);

        var update = await SendAsync(adminClient, HttpMethod.Put, $"/api/v1/users/{employee.UserId}", adminToken,
            UpdateBody("users-emp-renamed"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var me = await SendAsync(employeeClient, HttpMethod.Get, "/api/v1/auth/me", employee.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        var renamedLogin = await PostLoginAsync(employeeClient, "users-emp-renamed", Password);
        Assert.Equal(HttpStatusCode.OK, renamedLogin.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutCredentialChanges_KeepsExistingSessions()
    {
        var adminClient = _factory.CreateClient();
        var adminToken = (await LoginAsync(adminClient, "users-admin-keep", Role.Administrator)).AccessToken;
        var employeeClient = _factory.CreateClient();
        var employee = await LoginAsync(employeeClient, "users-emp-keep", Role.Employee);

        var update = await SendAsync(adminClient, HttpMethod.Put, $"/api/v1/users/{employee.UserId}", adminToken,
            UpdateBody("users-emp-keep", fullName: "Renamed Display Name"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var me = await SendAsync(employeeClient, HttpMethod.Get, "/api/v1/auth/me", employee.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Update_WithTooShortPassword_IsRejected()
    {
        var adminClient = _factory.CreateClient();
        var adminToken = (await LoginAsync(adminClient, "users-admin-short", Role.Administrator)).AccessToken;
        var employee = await LoginAsync(_factory.CreateClient(), "users-emp-short", Role.Employee);

        var update = await SendAsync(adminClient, HttpMethod.Put, $"/api/v1/users/{employee.UserId}", adminToken,
            UpdateBody("users-emp-short", password: "123"));

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Update_WithBlankPassword_IsRejected()
    {
        var adminClient = _factory.CreateClient();
        var adminToken = (await LoginAsync(adminClient, "users-admin-blank", Role.Administrator)).AccessToken;
        var employee = await LoginAsync(_factory.CreateClient(), "users-emp-blank", Role.Employee);

        var update = await SendAsync(adminClient, HttpMethod.Put, $"/api/v1/users/{employee.UserId}", adminToken,
            UpdateBody("users-emp-blank", password: "      "));

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Update_WithNewRole_EndsExistingSessions()
    {
        var adminClient = _factory.CreateClient();
        var adminToken = (await LoginAsync(adminClient, "users-admin-role", Role.Administrator)).AccessToken;
        var demotedClient = _factory.CreateClient();
        var demoted = await LoginAsync(demotedClient, "users-demoted-admin", Role.Administrator);

        var update = await SendAsync(adminClient, HttpMethod.Put, $"/api/v1/users/{demoted.UserId}", adminToken,
            UpdateBody("users-demoted-admin", role: "Employee"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        // The old Administrator token no longer reaches admin-only endpoints.
        var list = await SendAsync(demotedClient, HttpMethod.Get, "/api/v1/users", demoted.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    [Fact]
    public async Task Deactivate_ThenReactivate_DoesNotReviveOldSessions()
    {
        var adminClient = _factory.CreateClient();
        var adminToken = (await LoginAsync(adminClient, "users-admin-status", Role.Administrator)).AccessToken;
        var employeeClient = _factory.CreateClient();
        var employee = await LoginAsync(employeeClient, "users-emp-status", Role.Employee);

        var deactivate = await SendAsync(adminClient, HttpMethod.Patch, $"/api/v1/users/{employee.UserId}/status",
            adminToken, new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var reactivate = await SendAsync(adminClient, HttpMethod.Patch, $"/api/v1/users/{employee.UserId}/status",
            adminToken, new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);

        var me = await SendAsync(employeeClient, HttpMethod.Get, "/api/v1/auth/me", employee.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refresh.Headers.Add("Cookie", $"taskflow_refresh={employee.RefreshCookie}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await employeeClient.SendAsync(refresh)).StatusCode);

        var login = await PostLoginAsync(employeeClient, "users-emp-status", Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private static object UpdateBody(
        string username,
        string? password = null,
        string fullName = "Updated Name",
        string role = "Employee") => new
    {
        employeeCode = (string?)null,
        fullName,
        username,
        email = $"{username}@taskflow.local",
        role,
        departmentId = (int?)null,
        password
    };

    private async Task<Session> LoginAsync(HttpClient client, string username, Role role)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.local", Password, role);

        var response = await PostLoginAsync(client, username, Password);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var refreshHeader = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("taskflow_refresh=", StringComparison.OrdinalIgnoreCase));
        var pair = refreshHeader.Split(';')[0];

        return new Session(
            document.RootElement.GetProperty("user").GetProperty("id").GetInt32(),
            document.RootElement.GetProperty("accessToken").GetString()!,
            pair[(pair.IndexOf('=') + 1)..]);
    }

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string username, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password });

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return await client.SendAsync(request);
    }

    private sealed record Session(int UserId, string AccessToken, string RefreshCookie);
}
