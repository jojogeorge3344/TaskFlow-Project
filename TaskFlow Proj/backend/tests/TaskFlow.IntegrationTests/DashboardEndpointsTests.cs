using System.Net;
using TaskFlow.Domain.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

public class DashboardEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public DashboardEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminEndpoint_AsAdmin_ReturnsStats()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "dash-admin", SystemRoles.Administrator);

        var response = await GetWithTokenAsync(client, "/api/v1/dashboard/admin", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(document.RootElement.TryGetProperty("totalEmployees", out _));
        Assert.True(document.RootElement.TryGetProperty("totalDepartments", out _));
        Assert.True(document.RootElement.TryGetProperty("pendingTasks", out _));
    }

    [Fact]
    public async Task AdminEndpoint_AsEmployee_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "dash-employee", SystemRoles.Employee);

        var response = await GetWithTokenAsync(client, "/api/v1/dashboard/admin", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ManagerEndpoint_AsManager_ReturnsStats()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "dash-manager", SystemRoles.Manager);

        var response = await GetWithTokenAsync(client, "/api/v1/dashboard/manager", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(document.RootElement.TryGetProperty("tasksDueToday", out _));
        Assert.True(document.RootElement.TryGetProperty("overdueTasks", out _));
    }

    [Fact]
    public async Task ManagerEndpoint_AsEmployee_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "dash-employee", SystemRoles.Employee);

        var response = await GetWithTokenAsync(client, "/api/v1/dashboard/manager", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeEndpoint_AsEmployee_ReturnsStats()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "dash-employee", SystemRoles.Employee);

        var response = await GetWithTokenAsync(client, "/api/v1/dashboard/employee", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(document.RootElement.TryGetProperty("assignedTasks", out _));
        Assert.True(document.RootElement.TryGetProperty("inProgressTasks", out _));
    }

    [Fact]
    public async Task ManagerEndpoint_AsAdmin_IsAllowed()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "dash-admin", SystemRoles.Administrator);

        var response = await GetWithTokenAsync(client, "/api/v1/dashboard/manager", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeEndpoint_WithoutToken_IsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dashboard/employee");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<string> LoginAsync(HttpClient client, string username, string role)
    {
        const string password = "P@ssw0rd!";
        await _factory.SeedUserAsync(username, $"{username}@taskflow.local", password, role);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            emailOrUsername = username,
            password
        });

        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> GetWithTokenAsync(HttpClient client, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
