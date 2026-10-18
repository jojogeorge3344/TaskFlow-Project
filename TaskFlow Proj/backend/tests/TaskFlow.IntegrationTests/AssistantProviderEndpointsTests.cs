using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Application.Assistant;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

public class AssistantProviderEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Base = "/api/v1/assistant/providers";

    private readonly TaskFlowApiFactory _factory;

    public AssistantProviderEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("ai-cfg-get-employee", Role.Employee)]
    [InlineData("ai-cfg-get-manager", Role.Manager)]
    public async Task NonAdministrators_AreForbiddenFromTheAdminEndpoints(string username, Role role)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, username, role);

        var get = await SendAsync(client, HttpMethod.Get, $"{Base}/config", token);
        var put = await SendAsync(client, HttpMethod.Put, $"{Base}/config", token, JsonContent.Create(new { providers = Array.Empty<object>() }));
        var test = await SendAsync(client, HttpMethod.Post, $"{Base}/Ollama/test", token);

        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, test.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Base)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Base}/config")).StatusCode);
    }

    [Fact]
    public async Task Administrator_SavesAndTheKeyNeverAppearsInAnyResponseOrLog()
    {
        const string key = "sk-proj-INTEGRATIONSECRET-4321";
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "ai-cfg-admin", Role.Administrator);
        var employee = await LoginAsync(client, "ai-cfg-reader", Role.Employee);

        var put = await SendAsync(client, HttpMethod.Put, $"{Base}/config", admin, JsonContent.Create(new
        {
            providers = new object[]
            {
                new { kind = "Ollama", isEnabled = true, modelName = "llama3.1:8b", baseUrl = "http://localhost:11434" },
                new { kind = "OpenAI", isEnabled = true, modelName = "gpt-4o-mini", apiKey = key }
            }
        }));
        var putBody = await put.Content.ReadAsStringAsync();

        var adminGet = await SendAsync(client, HttpMethod.Get, $"{Base}/config", admin);
        var adminBody = await adminGet.Content.ReadAsStringAsync();
        var userGet = await SendAsync(client, HttpMethod.Get, Base, employee);
        var userBody = await userGet.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        foreach (var body in new[] { putBody, adminBody, userBody })
        {
            Assert.DoesNotContain("INTEGRATIONSECRET", body);
            Assert.DoesNotContain(key, body);
        }

        using (var config = JsonDocument.Parse(adminBody))
        {
            var openAi = config.RootElement.EnumerateArray().Single(p => p.GetProperty("kind").GetString() == "OpenAI");
            Assert.True(openAi.GetProperty("keySet").GetBoolean());
            Assert.Equal("4321", openAi.GetProperty("keyLast4").GetString());
            Assert.True(openAi.GetProperty("isConfigured").GetBoolean());
        }

        using (var available = JsonDocument.Parse(userBody))
        {
            var kinds = available.RootElement.EnumerateArray().Select(p => p.GetProperty("kind").GetString()).ToList();
            Assert.Contains("Ollama", kinds);
            Assert.Contains("OpenAI", kinds);
        }

        Assert.DoesNotContain(_factory.Logs.Lines, line => line.Contains("INTEGRATIONSECRET"));
    }

    [WindowsOnlyFact]
    public async Task OnWindowsTheKeyRingIsProtectedWithDpapiAndTheKeyStillRoundTrips()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "ai-cfg-admin-dpapi", Role.Administrator);

        var put = await SendAsync(client, HttpMethod.Put, $"{Base}/config", admin, JsonContent.Create(new
        {
            providers = new object[] { new { kind = "OpenAI", isEnabled = true, modelName = "gpt-4o-mini", apiKey = "sk-proj-DPAPITESTKEY-5555" } }
        }));
        var config = await JsonOf(await SendAsync(client, HttpMethod.Get, $"{Base}/config", admin));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.True(config.EnumerateArray().Single(p => p.GetProperty("kind").GetString() == "OpenAI").GetProperty("isConfigured").GetBoolean());
        var keyFiles = Directory.GetFiles(_factory.KeyRingPath, "key-*.xml");
        Assert.NotEmpty(keyFiles);
        foreach (var file in keyFiles)
        {
            var xml = await File.ReadAllTextAsync(file);
            Assert.Contains("encryptedKey", xml);
            Assert.DoesNotContain("masterKey", xml);
        }
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    [Fact]
    public async Task InvalidInput_Returns400()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "ai-cfg-admin-bad", Role.Administrator);

        var response = await SendAsync(client, HttpMethod.Put, $"{Base}/config", admin, JsonContent.Create(new
        {
            providers = new object[] { new { kind = "Ollama", isEnabled = true, modelName = "", baseUrl = "ftp://x" } }
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestConnection_ReportsSuccessAndClassifiedFailures()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "ai-cfg-admin-test", Role.Administrator);
        await SendAsync(client, HttpMethod.Put, $"{Base}/config", admin, JsonContent.Create(new
        {
            providers = new object[]
            {
                new { kind = "Gemini", isEnabled = true, modelName = "gemini-2.0-flash", apiKey = "AIzaSyTestKey0001" },
                new { kind = "OpenAI", isEnabled = false, modelName = "gpt-4o-mini" }
            }
        }));
        var gemini = _factory.Providers[AiProviderKind.Gemini];

        gemini.Handler = (_, _) => ChatModelResult.FromText("OK");
        var ok = await ReadAsync(await SendAsync(client, HttpMethod.Post, $"{Base}/Gemini/test", admin));

        gemini.Handler = (_, _) => throw new ProviderFailureException(AiProviderKind.Gemini, ProviderFailureKind.RateLimited);
        var limited = await ReadAsync(await SendAsync(client, HttpMethod.Post, $"{Base}/Gemini/test", admin));

        var notConfigured = await ReadAsync(await SendAsync(client, HttpMethod.Post, $"{Base}/OpenAI/test", admin));

        Assert.True(ok.GetProperty("ok").GetBoolean());
        Assert.False(limited.GetProperty("ok").GetBoolean());
        Assert.Equal("RateLimited", limited.GetProperty("failureKind").GetString());
        Assert.Contains("limit", limited.GetProperty("message").GetString());
        Assert.False(notConfigured.GetProperty("ok").GetBoolean());
        Assert.Contains("not configured", notConfigured.GetProperty("message").GetString());
        gemini.Reset();
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
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
