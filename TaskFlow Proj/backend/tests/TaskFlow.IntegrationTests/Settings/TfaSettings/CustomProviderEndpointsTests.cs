using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests.Settings.TfaSettings;

/// <summary>A custom provider from end to end: created on Add AI model (POST /providers/models with newProvider), used
/// by the TFA window, answering through the OpenAI-compatible client at its own address, and stored on each message.</summary>
public class CustomProviderEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Providers = "/api/v1/assistant/providers";
    private const string Chat = "/api/v1/assistant/conversation/messages";
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public CustomProviderEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ACustomProviderIsCreatedOnAPassingTestAndAnswersTheTfaWindow()
    {
        const string key = "gsk-CUSTOMPROVIDERSECRET-8642";
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "custom-provider-admin", SystemRoles.Administrator);
        var openAi = _factory.Providers[AiApiType.OpenAI];
        var runtimes = new List<AiProviderRuntime>();
        openAi.Handler = (runtime, _) =>
        {
            runtimes.Add(runtime);
            return ChatModelResult.FromText("Hello from Groq");
        };

        var add = await SendAsync(client, HttpMethod.Post, $"{Providers}/models", admin, new
        {
            modelName = "llama-3.3-70b-versatile",
            apiKey = key,
            newProvider = new { name = "Groq", apiType = "OpenAI", baseUrl = "https://api.groq.com/openai/v1", locality = "Cloud" }
        });
        var addBody = await add.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        Assert.DoesNotContain("CUSTOMPROVIDERSECRET", addBody);
        using (var result = JsonDocument.Parse(addBody))
        {
            Assert.True(result.RootElement.GetProperty("ok").GetBoolean());
            var groq = result.RootElement.GetProperty("providers").EnumerateArray().Single(provider => provider.GetProperty("displayName").GetString() == "Groq");
            Assert.False(groq.GetProperty("isBuiltIn").GetBoolean());
            Assert.True(groq.GetProperty("isEnabled").GetBoolean());
            Assert.Equal("8642", groq.GetProperty("keyLast4").GetString());
        }

        var groqId = await GroqIdAsync();
        var reply = await SendAsync(client, HttpMethod.Post, Chat, admin, new { text = "hello", providerId = groqId });
        var replyBody = await reply.Content.ReadAsStringAsync();
        openAi.Reset();

        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        using (var chat = JsonDocument.Parse(replyBody))
        {
            var answer = chat.RootElement.GetProperty("messages")[1];
            Assert.Equal("Hello from Groq", answer.GetProperty("content").GetString());
            Assert.Equal("Groq", answer.GetProperty("provider").GetString());
            Assert.Equal(groqId, answer.GetProperty("providerId").GetInt32());
        }

        var chatRuntime = runtimes.Last();
        Assert.Equal("https://api.groq.com/openai/v1", chatRuntime.BaseUrl);
        Assert.Equal("Groq", chatRuntime.Name);
        Assert.Equal(key, chatRuntime.ApiKey);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.AssistantMessages.AsNoTracking().OrderByDescending(message => message.Id).FirstAsync();
        Assert.Equal(groqId, stored.ProviderId);
    }

    [Fact]
    public async Task AnUnknownProviderIdIs404AndAGeminiTypeCustomProviderIsRefused()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "custom-provider-admin-404", SystemRoles.Administrator);

        var unknown = await SendAsync(client, HttpMethod.Post, $"{Providers}/999/models/use", admin, new { modelName = "x" });
        var geminiType = await SendAsync(client, HttpMethod.Post, $"{Providers}/models", admin, new
        {
            modelName = "gemini-3.8-flash",
            newProvider = new { name = "Second Gemini", apiType = "Gemini", baseUrl = "https://example.com", locality = "Cloud" }
        });

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, geminiType.StatusCode);
        Assert.Contains("A custom provider can use the OpenAI-compatible or Ollama API.", await geminiType.Content.ReadAsStringAsync());
    }

    private async Task<int> GroqIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.AiProviderConfigs.AsNoTracking().SingleAsync(row => row.Name == "Groq")).Id;
    }

    private async Task<string> LoginAsync(HttpClient client, string username, string role)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, role);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = Password });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string token, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
