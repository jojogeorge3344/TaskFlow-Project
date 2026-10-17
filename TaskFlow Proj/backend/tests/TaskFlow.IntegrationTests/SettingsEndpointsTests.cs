using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

public class SettingsEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Url = "/api/v1/settings/reminders";

    private readonly TaskFlowApiFactory _factory;

    public SettingsEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("set-get-manager", Role.Manager)]
    [InlineData("set-get-admin", Role.Administrator)]
    public async Task Get_ReturnsCurrentSettings_ForManagerAndAdmin(string username, Role role)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, username, role);

        var response = await SendAsync(client, HttpMethod.Get, Url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("enabled", out _));
        Assert.True(root.TryGetProperty("deadlineDayEnabled", out _));
        Assert.True(root.TryGetProperty("emailEnabled", out _));
        Assert.InRange(root.GetProperty("defaultFirstDays").GetInt32(), 1, 30);
        Assert.InRange(root.GetProperty("defaultSecondDays").GetInt32(), 0, 30);
        Assert.Contains(root.GetProperty("defaultOverdueFrequency").GetString(), new[] { "Daily", "Weekly" });
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("timezone").GetString()));
    }

    [Fact]
    public async Task Put_PersistsAndReturnsSavedValues_AndIgnoresTimezone()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "set-put-manager", Role.Manager);
        var before = await GetTimezoneAsync(client, token);

        var save = await SendAsync(client, HttpMethod.Put, Url, token, JsonContent.Create(new
        {
            enabled = true,
            deadlineDayEnabled = false,
            defaultFirstDays = 3,
            defaultSecondDays = 1,
            defaultOverdueFrequency = "Weekly",
            emailEnabled = true,
            timezone = "America/New_York"
        }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var reload = await SendAsync(client, HttpMethod.Get, Url, token);
        using (var document = await JsonDocument.ParseAsync(await reload.Content.ReadAsStreamAsync()))
        {
            var root = document.RootElement;
            Assert.False(root.GetProperty("deadlineDayEnabled").GetBoolean());
            Assert.Equal(3, root.GetProperty("defaultFirstDays").GetInt32());
            Assert.Equal(1, root.GetProperty("defaultSecondDays").GetInt32());
            Assert.Equal("Weekly", root.GetProperty("defaultOverdueFrequency").GetString());
            Assert.True(root.GetProperty("emailEnabled").GetBoolean());
            Assert.Equal(before, root.GetProperty("timezone").GetString());
        }

        // Restore the defaults so other tests in this class see a predictable state.
        var restore = await SendAsync(client, HttpMethod.Put, Url, token, DefaultsBody());
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
    }

    [Fact]
    public async Task Put_InvalidValues_Returns400WithFieldErrors_AndChangesNothing()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "set-invalid-admin", Role.Administrator);
        var before = await (await SendAsync(client, HttpMethod.Get, Url, token)).Content.ReadAsStringAsync();

        var outOfRange = await SendAsync(client, HttpMethod.Put, Url, token, JsonContent.Create(new
        {
            enabled = false,
            deadlineDayEnabled = true,
            defaultFirstDays = 45,
            defaultSecondDays = 1,
            defaultOverdueFrequency = "Daily",
            emailEnabled = false
        }));
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);

        using (var document = await JsonDocument.ParseAsync(await outOfRange.Content.ReadAsStreamAsync()))
        {
            Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("DefaultFirstDays", out _));
        }

        var wrongOrder = await SendAsync(client, HttpMethod.Put, Url, token, JsonContent.Create(new
        {
            enabled = false,
            deadlineDayEnabled = true,
            defaultFirstDays = 1,
            defaultSecondDays = 2,
            defaultOverdueFrequency = "Daily",
            emailEnabled = false
        }));
        Assert.Equal(HttpStatusCode.BadRequest, wrongOrder.StatusCode);

        using (var document = await JsonDocument.ParseAsync(await wrongOrder.Content.ReadAsStreamAsync()))
        {
            Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("DefaultSecondDays", out _));
        }

        var after = await (await SendAsync(client, HttpMethod.Get, Url, token)).Content.ReadAsStringAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Put_MissingFields_Returns400_AndDoesNotSwitchRemindersOff()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "set-partial-manager", Role.Manager);

        var response = await SendAsync(client, HttpMethod.Put, Url, token, JsonContent.Create(new
        {
            defaultFirstDays = 3,
            defaultSecondDays = 1
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using (var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        {
            var errors = document.RootElement.GetProperty("errors");
            Assert.True(errors.TryGetProperty("Enabled", out _));
            Assert.True(errors.TryGetProperty("DefaultOverdueFrequency", out _));
        }

        var reload = await SendAsync(client, HttpMethod.Get, Url, token);
        using var current = await JsonDocument.ParseAsync(await reload.Content.ReadAsStreamAsync());
        Assert.True(current.RootElement.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Employee_IsForbidden()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "set-employee", Role.Employee);

        var get = await SendAsync(client, HttpMethod.Get, Url, token);
        var put = await SendAsync(client, HttpMethod.Put, Url, token, DefaultsBody());

        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static JsonContent DefaultsBody() => JsonContent.Create(new
    {
        enabled = true,
        deadlineDayEnabled = true,
        defaultFirstDays = 2,
        defaultSecondDays = 1,
        defaultOverdueFrequency = "Daily",
        emailEnabled = false
    });

    private static async Task<string?> GetTimezoneAsync(HttpClient client, string token)
    {
        var response = await SendAsync(client, HttpMethod.Get, Url, token);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("timezone").GetString();
    }

    private async Task<string> LoginAsync(HttpClient client, string username, Role role)
    {
        const string password = "P@ssw0rd!";
        await _factory.SeedUserAsync(username, $"{username}@taskflow.local", password, role);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string token,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
