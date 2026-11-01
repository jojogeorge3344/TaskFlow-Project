using TaskFlow.Api.Common.Constants;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace TaskFlow.IntegrationTests.Settings.TfaSettings;

/// <summary>A first run with a Gemini key supplied in configuration, as a developer sets it with user secrets.</summary>
public class ConfiguredGeminiKeyFactory : TaskFlowApiFactory
{
    public const string Key = "AIzaSyFirstRunConfigured9876";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:GeminiApiKey"] = Key
        }));
    }
}

/// <summary>With a configured key, Google Gemini is the working default from the first run.</summary>
public class GeminiDefaultOnFirstRunTests : IClassFixture<ConfiguredGeminiKeyFactory>
{
    private readonly ConfiguredGeminiKeyFactory _factory;

    public GeminiDefaultOnFirstRunTests(ConfiguredGeminiKeyFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ANewDatabaseWithAConfiguredKeyStartsWithGeminiOnAndOllamaOff()
    {
        var client = _factory.CreateClient();
        var admin = await AiDefaultsOnFirstRunTests.LoginAsync(_factory, client, "gemini-first-run-admin", SystemRoles.Administrator);

        var config = await AiDefaultsOnFirstRunTests.GetAsync(client, "/api/v1/assistant/providers/config", admin);

        Assert.Equal("Gemini", config[0].GetProperty("apiType").GetString());
        var gemini = config[0];
        Assert.True(gemini.GetProperty("isEnabled").GetBoolean());
        Assert.True(gemini.GetProperty("isConfigured").GetBoolean());
        Assert.True(gemini.GetProperty("keySet").GetBoolean());
        Assert.Equal("9876", gemini.GetProperty("keyLast4").GetString());
        Assert.DoesNotContain(ConfiguredGeminiKeyFactory.Key, config.GetRawText());

        var ollama = config.EnumerateArray().Single(provider => provider.GetProperty("apiType").GetString() == "Ollama");
        Assert.False(ollama.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public async Task EveryoneSignedInCanUseTfaOnTheFirstRunWithGemini()
    {
        var client = _factory.CreateClient();
        var employee = await AiDefaultsOnFirstRunTests.LoginAsync(_factory, client, "gemini-first-run-employee", SystemRoles.Employee);

        var available = await AiDefaultsOnFirstRunTests.GetAsync(client, "/api/v1/assistant/providers", employee);

        var only = Assert.Single(available.EnumerateArray());
        Assert.Equal("Google Gemini", only.GetProperty("displayName").GetString());
        Assert.Equal("Cloud", only.GetProperty("locality").GetString());
    }
}

/// <summary>A brand-new database (the first run on someone else's machine) comes up with the AI defaults already in it,
/// with nobody having to configure anything. Without a configured Gemini key no provider is switched on. Its own factory means
/// its own empty database.</summary>
public class AiDefaultsOnFirstRunTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public AiDefaultsOnFirstRunTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ANewDatabaseStartsWithTheModelListGeminiFirstAndNothingOnWithoutAKey()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(_factory, client, "ai-first-run-admin", SystemRoles.Administrator);

        var config = await GetAsync(client, "/api/v1/assistant/providers/config", admin);

        Assert.Equal(3, config.GetArrayLength());
        foreach (var provider in config.EnumerateArray())
        {
            var kind = Enum.Parse<AiApiType>(provider.GetProperty("apiType").GetString()!);
            var models = provider.GetProperty("models").EnumerateArray().Select(model => model.GetString()!).ToList();
            Assert.Equal(AiProviderCatalog.BuiltInModels(kind), models);
            Assert.False(provider.GetProperty("keySet").GetBoolean());
        }

        Assert.Equal("Gemini", config[0].GetProperty("apiType").GetString());
        Assert.All(config.EnumerateArray(), provider => Assert.False(provider.GetProperty("isEnabled").GetBoolean()));

        var ollama = config.EnumerateArray().Single(provider => provider.GetProperty("apiType").GetString() == "Ollama");
        Assert.Equal("llama3.2:3b", ollama.GetProperty("modelName").GetString());
        Assert.Equal("http://localhost:11434", ollama.GetProperty("baseUrl").GetString());
    }

    [Fact]
    public async Task WithoutAConfiguredKeyNoProviderIsAvailableOnTheFirstRun()
    {
        var client = _factory.CreateClient();
        var employee = await LoginAsync(_factory, client, "ai-first-run-employee", SystemRoles.Employee);

        var available = await GetAsync(client, "/api/v1/assistant/providers", employee);

        Assert.Empty(available.EnumerateArray());
    }

    internal static async System.Threading.Tasks.Task<string> LoginAsync(TaskFlowApiFactory factory, HttpClient client, string username, string role)
    {
        const string password = "P@ssw0rd!";
        await factory.SeedUserAsync(username, $"{username}@taskflow.com", password, role);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    internal static async System.Threading.Tasks.Task<JsonElement> GetAsync(HttpClient client, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
