using System.Net;
using TaskFlow.Api.Common.Security;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.IntegrationTests.Settings.ApplicationSettings;

public class ApplicationSettingsEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Url = "/api/v1/settings/application";

    private readonly TaskFlowApiFactory _factory;

    public ApplicationSettingsEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("app-get-employee", SystemRoles.Employee)]
    [InlineData("app-get-manager", SystemRoles.Manager)]
    [InlineData("app-get-admin", SystemRoles.Administrator)]
    public async Task Get_ReturnsSettings_ForEverySignedInRole(string username, string role)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, username, role);

        var response = await SendAsync(client, HttpMethod.Get, Url, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("TaskFlow", document.RootElement.GetProperty("name").GetString());
        Assert.Equal("Medium", document.RootElement.GetProperty("defaultPriority").GetString());
        Assert.True(document.RootElement.GetProperty("inAppNotificationsEnabled").GetBoolean());
    }

    [Fact]
    public async Task Put_PersistsAndReturnsSavedValues()
    {
        var client = _factory.CreateClient();
        var adminToken = await LoginAsync(client, "app-admin", SystemRoles.Administrator);
        var body = JsonContent.Create(new
        {
            name = "Acme Tasks",
            timezone = "UTC",
            defaultPriority = "High",
            inAppNotificationsEnabled = false
        });

        var put = await SendAsync(client, HttpMethod.Put, Url, adminToken, body);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using (var saved = JsonDocument.Parse(await put.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Acme Tasks", saved.RootElement.GetProperty("name").GetString());
            Assert.Equal("UTC", saved.RootElement.GetProperty("timezone").GetString());
            Assert.Equal("High", saved.RootElement.GetProperty("defaultPriority").GetString());
            Assert.False(saved.RootElement.GetProperty("inAppNotificationsEnabled").GetBoolean());
        }

        var get = await SendAsync(client, HttpMethod.Get, Url, adminToken);
        using var reloaded = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.Equal("Acme Tasks", reloaded.RootElement.GetProperty("name").GetString());

        // Restore the defaults for the shared database.
        var restore = JsonContent.Create(new
        {
            name = "TaskFlow",
            timezone = "Asia/Kolkata",
            defaultPriority = "Medium",
            inAppNotificationsEnabled = true
        });
        await SendAsync(client, HttpMethod.Put, Url, adminToken, restore);
    }

    [Fact]
    public async Task Manager_IsForbiddenFromWriting()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "app-manager-write", SystemRoles.Manager);
        var body = JsonContent.Create(new
        {
            name = "Nope",
            timezone = "UTC",
            defaultPriority = "Low",
            inAppNotificationsEnabled = true
        });

        var response = await SendAsync(client, HttpMethod.Put, Url, token, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnknownTimezone_Returns400WithFieldError_AndSavesNothing()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "app-admin-bad-tz", SystemRoles.Administrator);
        var body = JsonContent.Create(new
        {
            name = "Broken",
            timezone = "Mars/Olympus_Mons",
            defaultPriority = "Low",
            inAppNotificationsEnabled = true
        });

        var response = await SendAsync(client, HttpMethod.Put, Url, token, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("Timezone", out _));

        var get = await SendAsync(client, HttpMethod.Get, Url, token);
        using var current = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.NotEqual("Broken", current.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task BlankName_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "app-admin-blank-name", SystemRoles.Administrator);
        var body = JsonContent.Create(new
        {
            name = "   ",
            timezone = "UTC",
            defaultPriority = "Low",
            inAppNotificationsEnabled = true
        });

        var response = await SendAsync(client, HttpMethod.Put, Url, token, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var get = await client.GetAsync(Url);
        var put = await client.PutAsJsonAsync(Url, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    private async Task<string> LoginAsync(HttpClient client, string username, string role)
    {
        const string password = "P@ssw0rd!";
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", password, role);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string url, string token, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
