using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

public class UserProfileEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string MeUrl = "/api/v1/users/me";

    private readonly TaskFlowApiFactory _factory;

    public UserProfileEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMe_ReturnsOwnProfileOnly()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "prof-reader", Role.Employee);
        await _factory.SeedUserAsync("prof-victim", "prof-victim@taskflow.local", "P@ssw0rd!", Role.Manager);

        var response = await SendAsync(client, HttpMethod.Get, MeUrl, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("prof-reader", document.RootElement.GetProperty("username").GetString());
        Assert.Equal("prof-reader@taskflow.local", document.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task PutMe_UpdatesNameAndEmail()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "prof-edit", Role.Employee);

        var response = await SendAsync(client, HttpMethod.Put, MeUrl, token,
            JsonContent.Create(new { fullName = "Prof Edited", email = "prof-edited@taskflow.local" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Prof Edited", document.RootElement.GetProperty("fullName").GetString());
        Assert.Equal("prof-edited@taskflow.local", document.RootElement.GetProperty("email").GetString());

        // Username and role stay untouched by self-service.
        Assert.Equal("prof-edit", document.RootElement.GetProperty("username").GetString());
        Assert.Equal("Employee", document.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task PutMe_DuplicateEmail_Returns400()
    {
        var client = _factory.CreateClient();
        await _factory.SeedUserAsync("prof-taken-owner", "taken@taskflow.local", "P@ssw0rd!", Role.Employee);
        var token = await LoginAsync(client, "prof-taken", Role.Employee);

        var response = await SendAsync(client, HttpMethod.Put, MeUrl, token,
            JsonContent.Create(new { fullName = "Prof Taken", email = "taken@taskflow.local" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_MalformedEmail_Returns400WithFieldError()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "prof-bad-email", Role.Employee);

        var response = await SendAsync(client, HttpMethod.Put, MeUrl, token,
            JsonContent.Create(new { fullName = "Bad Email", email = "not-an-email" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task PutMe_CannotEscalateRole()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "prof-escalate", Role.Employee);

        // The contract does not even carry role/username, but prove a smuggled payload changes nothing.
        var response = await SendAsync(client, HttpMethod.Put, MeUrl, token,
            JsonContent.Create(new
            {
                fullName = "Still Employee",
                email = "prof-escalate@taskflow.local",
                role = "Administrator",
                username = "hacked",
                isActive = false
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await SendAsync(client, HttpMethod.Get, MeUrl, token);
        using var document = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        Assert.Equal("Employee", document.RootElement.GetProperty("role").GetString());
        Assert.Equal("prof-escalate", document.RootElement.GetProperty("username").GetString());
        Assert.True(document.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Password_WrongCurrent_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "prof-wrong-pw", Role.Employee);

        var response = await SendAsync(client, HttpMethod.Post, $"{MeUrl}/password", token,
            JsonContent.Create(new { currentPassword = "not-the-password", newPassword = "N3wP@ss!" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Password_ShortNewPassword_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "prof-short-pw", Role.Employee);

        var response = await SendAsync(client, HttpMethod.Post, $"{MeUrl}/password", token,
            JsonContent.Create(new { currentPassword = "P@ssw0rd!", newPassword = "abc" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Password_OverlongNewPassword_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "prof-long-pw", Role.Employee);
        var tooLong = new string('x', 129);

        var response = await SendAsync(client, HttpMethod.Post, $"{MeUrl}/password", token,
            JsonContent.Create(new { currentPassword = "P@ssw0rd!", newPassword = tooLong }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Password_Change_Returns204_AndEndsSessions()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "prof-pw-change", Role.Employee);

        var response = await SendAsync(client, HttpMethod.Post, $"{MeUrl}/password", token,
            JsonContent.Create(new { currentPassword = "P@ssw0rd!", newPassword = "N3wP@ss!" }));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The old access token no longer passes validation (the security stamp changed).
        var oldTokenCall = await SendAsync(client, HttpMethod.Get, MeUrl, token);
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenCall.StatusCode);

        // Signing in with the new password works.
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { emailOrUsername = "prof-pw-change", password = "N3wP@ss!" });
        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MeUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(MeUrl, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"{MeUrl}/password", new { })).StatusCode);
    }

    private async Task<string> LoginAsync(HttpClient client, string username, Role role)
    {
        const string password = "P@ssw0rd!";
        await _factory.SeedUserAsync(username, $"{username}@taskflow.local", password, role);
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
