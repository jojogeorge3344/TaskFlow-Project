using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Application.Assistant;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;

namespace TaskFlow.IntegrationTests;

/// <summary>A brand-new database (the first run on someone else's machine) comes up with the AI defaults already in it,
/// with nobody having to configure anything. Its own factory means its own empty database.</summary>
public class AiDefaultsOnFirstRunTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public AiDefaultsOnFirstRunTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ANewDatabaseStartsWithTheModelListAndOllamaReady()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "ai-first-run-admin", SystemRoles.Administrator);

        var config = await GetAsync(client, "/api/v1/assistant/providers/config", admin);

        Assert.Equal(3, config.GetArrayLength());
        foreach (var provider in config.EnumerateArray())
        {
            var kind = Enum.Parse<AiProviderKind>(provider.GetProperty("kind").GetString()!);
            var models = provider.GetProperty("models").EnumerateArray().Select(model => model.GetString()!).ToList();
            Assert.Equal(AiProviderCatalog.BuiltInModels(kind), models);
            Assert.False(provider.GetProperty("keySet").GetBoolean());
        }

        var ollama = config.EnumerateArray().Single(provider => provider.GetProperty("kind").GetString() == "Ollama");
        Assert.True(ollama.GetProperty("isEnabled").GetBoolean());
        Assert.True(ollama.GetProperty("isConfigured").GetBoolean());
        Assert.Equal("llama3.2:3b", ollama.GetProperty("modelName").GetString());
        Assert.Equal("http://localhost:11434", ollama.GetProperty("baseUrl").GetString());
    }

    [Fact]
    public async Task EveryoneSignedInCanUseTfaOnTheFirstRunBecauseOllamaIsOn()
    {
        var client = _factory.CreateClient();
        var employee = await LoginAsync(client, "ai-first-run-employee", SystemRoles.Employee);

        var available = await GetAsync(client, "/api/v1/assistant/providers", employee);

        var only = Assert.Single(available.EnumerateArray());
        Assert.Equal("Ollama", only.GetProperty("kind").GetString());
    }

    private async System.Threading.Tasks.Task<string> LoginAsync(HttpClient client, string username, string role)
    {
        const string password = "P@ssw0rd!";
        await _factory.SeedUserAsync(username, $"{username}@taskflow.local", password, role);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async System.Threading.Tasks.Task<JsonElement> GetAsync(HttpClient client, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
