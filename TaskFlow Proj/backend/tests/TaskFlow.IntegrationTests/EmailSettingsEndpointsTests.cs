using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Application.Email;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

public class EmailSettingsEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Url = "/api/v1/settings/email";

    private readonly TaskFlowApiFactory _factory;

    public EmailSettingsEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_ReturnsConfiguration_WithPasswordIndicatorButNoPassword()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "email-get-admin", Role.Administrator);

        var response = await SendAsync(client, HttpMethod.Get, Url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(TaskFlowApiFactory.SmtpPassword, body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.GetProperty("passwordConfigured").GetBoolean());
        Assert.Equal(JsonValueKind.Number, root.GetProperty("smtpPort").ValueKind);
        Assert.Contains(root.GetProperty("smtpSecurity").GetString(), new[] { "None", "StartTls", "SslOnConnect", "Auto" });
        Assert.Matches(@"^\d\d:\d\d$", root.GetProperty("sendTime").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("issues").ValueKind);
        Assert.False(root.TryGetProperty("smtpPassword", out _));
        Assert.False(root.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Put_PersistsConfiguration_AndRecordsWhoChangedIt()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "email-put-admin", Role.Administrator);

        var save = await SendAsync(client, HttpMethod.Put, Url, token, Body(host: "smtp.example.com", port: 2525, sendTime: "08:15"));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.DoesNotContain(TaskFlowApiFactory.SmtpPassword, await save.Content.ReadAsStringAsync());

        var reload = await SendAsync(client, HttpMethod.Get, Url, token);
        using var document = await JsonDocument.ParseAsync(await reload.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        Assert.Equal("smtp.example.com", root.GetProperty("smtpHost").GetString());
        Assert.Equal(2525, root.GetProperty("smtpPort").GetInt32());
        Assert.Equal("08:15", root.GetProperty("sendTime").GetString());
        Assert.Equal("taskflow@example.com", root.GetProperty("senderAddress").GetString());
        Assert.Equal(JsonValueKind.String, root.GetProperty("updatedAt").ValueKind);
        Assert.Equal("email-put-admin full name", root.GetProperty("updatedByName").GetString());
        Assert.Empty(root.GetProperty("issues").EnumerateArray());
    }

    [Fact]
    public async Task Put_EmailSwitchIsSharedWithReminderSettings()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "email-shared-admin", Role.Administrator);

        await SendAsync(client, HttpMethod.Put, Url, token, Body(enabled: true));
        var reminders = await SendAsync(client, HttpMethod.Get, "/api/v1/settings/reminders", token);
        using (var document = await JsonDocument.ParseAsync(await reminders.Content.ReadAsStreamAsync()))
        {
            Assert.True(document.RootElement.GetProperty("emailEnabled").GetBoolean());
        }

        await SendAsync(client, HttpMethod.Put, Url, token, Body(enabled: false));
    }

    [Theory]
    [InlineData(70000, "smtp.example.com", "smtpPort")]
    [InlineData(587, "", "smtpHost")]
    public async Task Put_InvalidValues_Returns400WithFieldErrors_AndChangesNothing(int port, string host, string field)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "email-invalid-admin", Role.Administrator);
        await SendAsync(client, HttpMethod.Put, Url, token, Body(host: "keep.example.com"));

        var response = await SendAsync(client, HttpMethod.Put, Url, token, Body(enabled: true, host: host, port: port));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using (var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        {
            var errors = document.RootElement.GetProperty("errors");
            Assert.Contains(errors.EnumerateObject(), error => string.Equals(error.Name, field, StringComparison.OrdinalIgnoreCase));
        }

        var reload = await SendAsync(client, HttpMethod.Get, Url, token);
        using var stored = await JsonDocument.ParseAsync(await reload.Content.ReadAsStreamAsync());
        Assert.Equal("keep.example.com", stored.RootElement.GetProperty("smtpHost").GetString());
        Assert.NotEqual(70000, stored.RootElement.GetProperty("smtpPort").GetInt32());
    }

    [Fact]
    public async Task Manager_IsForbidden_OnEveryEndpoint()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "email-manager", Role.Manager);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, Url, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Put, Url, token, Body())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Post, $"{Url}/test", token, JsonContent.Create(new { toAddress = (string?)null }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, $"{Url}/deliveries", token)).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Url}/deliveries")).StatusCode);
    }

    [Fact]
    public async Task Test_Succeeds_DefaultsToCallersEmail_AndIsLogged_EvenWhileEmailIsDisabled()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "email-test-ok-admin", Role.Administrator);
        await SendAsync(client, HttpMethod.Put, Url, token, Body(enabled: false));
        _factory.EmailSender.NextResult = EmailSendResult.Sent;

        var response = await SendAsync(client, HttpMethod.Post, $"{Url}/test", token, JsonContent.Create(new { toAddress = (string?)null }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        {
            Assert.True(document.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains("email-test-ok-admin@taskflow.local", document.RootElement.GetProperty("message").GetString());
        }

        Assert.Contains(_factory.EmailSender.Sent, message => message.ToAddress == "email-test-ok-admin@taskflow.local");
        var delivery = await FindDeliveryAsync(client, token, "email-test-ok-admin@taskflow.local");
        Assert.Equal("Test", delivery.GetProperty("kind").GetString());
        Assert.Equal("Sent", delivery.GetProperty("status").GetString());
        Assert.Equal(1, delivery.GetProperty("attempts").GetInt32());
    }

    [Fact]
    public async Task Test_Failure_Returns200WithReason_AndLogsFailedRow()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "email-test-fail-admin", Role.Administrator);
        await SendAsync(client, HttpMethod.Put, Url, token, Body());
        const string reason = "Authentication failed: the SMTP server rejected the username or password.";
        _factory.EmailSender.NextResult = EmailSendResult.Failed(reason);

        try
        {
            var response = await SendAsync(client, HttpMethod.Post, $"{Url}/test", token, JsonContent.Create(new { toAddress = "ops@example.com" }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(TaskFlowApiFactory.SmtpPassword, body);
            using var document = JsonDocument.Parse(body);
            Assert.False(document.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(reason, document.RootElement.GetProperty("message").GetString());

            var delivery = await FindDeliveryAsync(client, token, "ops@example.com");
            Assert.Equal("Failed", delivery.GetProperty("status").GetString());
            Assert.Equal(reason, delivery.GetProperty("lastError").GetString());
        }
        finally
        {
            _factory.EmailSender.NextResult = EmailSendResult.Sent;
        }
    }

    [Fact]
    public async Task Test_InvalidAddress_Returns400_AndSendsNothing()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "email-test-bad-admin", Role.Administrator);
        var sentBefore = _factory.EmailSender.Sent.Count;

        var response = await SendAsync(client, HttpMethod.Post, $"{Url}/test", token, JsonContent.Create(new { toAddress = "abc" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(sentBefore, _factory.EmailSender.Sent.Count);
        var deliveries = await SendAsync(client, HttpMethod.Get, $"{Url}/deliveries?take=100", token);
        Assert.DoesNotContain("\"abc\"", await deliveries.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Deliveries_AreNewestFirst_AndTakeIsClamped()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "email-list-admin", Role.Administrator);
        await SendAsync(client, HttpMethod.Put, Url, token, Body());
        await SendAsync(client, HttpMethod.Post, $"{Url}/test", token, JsonContent.Create(new { toAddress = "first@example.com" }));
        await SendAsync(client, HttpMethod.Post, $"{Url}/test", token, JsonContent.Create(new { toAddress = "second@example.com" }));

        var response = await SendAsync(client, HttpMethod.Get, $"{Url}/deliveries?take=0", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var items = document.RootElement.EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("second@example.com", items[0].GetProperty("toAddress").GetString());
        Assert.False(items[0].TryGetProperty("body", out _));
    }

    private static JsonContent Body(bool enabled = false, string host = "smtp.example.com", int port = 587, string sendTime = "09:00") =>
        JsonContent.Create(new
        {
            emailEnabled = enabled,
            smtpHost = host,
            smtpPort = port,
            smtpSecurity = "StartTls",
            senderAddress = "taskflow@example.com",
            senderName = "TaskFlow",
            smtpUsername = "",
            sendTime
        });

    private static async Task<JsonElement> FindDeliveryAsync(HttpClient client, string token, string address)
    {
        var response = await SendAsync(client, HttpMethod.Get, $"{Url}/deliveries?take=100", token);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.EnumerateArray().First(item => item.GetProperty("toAddress").GetString() == address).Clone();
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
