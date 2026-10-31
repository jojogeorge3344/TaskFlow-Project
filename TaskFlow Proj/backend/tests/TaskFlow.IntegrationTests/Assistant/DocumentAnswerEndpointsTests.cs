using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Services.Assistant.Documents;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests.Assistant;

/// <summary>A TFA message answered from the documents folder, through the real indexer, search, prompt and storage.</summary>
public class DocumentAnswerEndpointsTests : IClassFixture<DocumentKnowledgeFactory>
{
    private const string Base = "/api/v1/assistant";

    private readonly DocumentKnowledgeFactory _factory;

    public DocumentAnswerEndpointsTests(DocumentKnowledgeFactory factory)
    {
        _factory = factory;
        foreach (var provider in _factory.Providers.Values)
        {
            provider.Reset();
        }
    }

    internal static async Task<(HttpClient Client, string Token)> SignInAsync(TaskFlowApiFactory factory, string role)
    {
        var username = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}"[..20];
        await factory.SeedUserAsync(username, $"{username}@taskflow.com", "P@ssw0rd!", role);
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = "P@ssw0rd!" });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (client, document.RootElement.GetProperty("accessToken").GetString()!);
    }

    internal static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private async Task ConfigureOllamaAsync()
    {
        var (client, token) = await SignInAsync(_factory, SystemRoles.Administrator);
        var response = await SendAsync(client, HttpMethod.Put, $"{Base}/providers/config", token, JsonContent.Create(new
        {
            providers = new object[]
            {
                new { kind = "Ollama", isEnabled = true, modelName = "llama3.1:8b", baseUrl = "http://localhost:11434" },
                new { kind = "Gemini", isEnabled = false, modelName = "gemini-2.0-flash" },
                new { kind = "OpenAI", isEnabled = false, modelName = "gpt-4o-mini" }
            }
        }));
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AnEmployeesDocumentAnswerShowsItsSourcesAndKeepsThemAfterAReload()
    {
        await ConfigureOllamaAsync();
        Assert.True(await _factory.Services.GetRequiredService<DocumentIndexerService>().RunOnceAsync());
        _factory.Providers[AiProviderKind.Ollama].Handler = (_, _) => ChatModelResult.FromText("The assignee and the task's creator.");
        var (client, token) = await SignInAsync(_factory, SystemRoles.Employee);

        var sent = await SendAsync(client, HttpMethod.Post, $"{Base}/conversation/messages", token,
            JsonContent.Create(new { text = "who receives the overdue reminder?", provider = "Ollama" }));
        sent.EnsureSuccessStatusCode();
        using var reply = JsonDocument.Parse(await sent.Content.ReadAsStringAsync());
        var reloaded = await SendAsync(client, HttpMethod.Get, $"{Base}/conversation", token);
        using var conversation = JsonDocument.Parse(await reloaded.Content.ReadAsStringAsync());

        var prompt = _factory.Providers[AiProviderKind.Ollama].Calls.Single().Request.SystemPrompt;
        Assert.Contains("[1] FRD – Reminders › 4.2 Overdue reminder", prompt);
        Assert.Contains("sent to the assignee and the task's creator", prompt);
        Assert.DoesNotContain("Compose screen", prompt);

        foreach (var answer in new[] { reply.RootElement.GetProperty("messages")[1], conversation.RootElement.GetProperty("messages")[1] })
        {
            var source = Assert.Single(answer.GetProperty("sources").EnumerateArray());
            Assert.Equal("FRD – Reminders", source.GetProperty("document").GetString());
            Assert.Equal("4.2 Overdue reminder", source.GetProperty("section").GetString());
            Assert.Equal("TF - FRD/FRD-Reminders.md", source.GetProperty("path").GetString());
        }

        Assert.Empty(conversation.RootElement.GetProperty("messages")[0].GetProperty("sources").EnumerateArray());
    }
}
