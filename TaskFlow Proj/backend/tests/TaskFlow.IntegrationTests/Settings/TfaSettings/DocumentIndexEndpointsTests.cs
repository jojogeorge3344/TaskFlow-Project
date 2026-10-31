using System.Net;
using System.Text.Json;
using TaskFlow.Api.Common.Security;
using TaskFlow.IntegrationTests.Assistant;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests.Settings.TfaSettings;

/// <summary>The Document search card's endpoints (design D13).</summary>
public class DocumentIndexEndpointsTests : IClassFixture<DocumentKnowledgeFactory>
{
    private const string Base = "/api/v1/assistant/documents";

    private readonly DocumentKnowledgeFactory _factory;

    public DocumentIndexEndpointsTests(DocumentKnowledgeFactory factory)
    {
        _factory = factory;
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private async Task<JsonElement> WaitForStatusAsync(HttpClient client, string token, Func<JsonElement, bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            var status = await JsonOf(await DocumentAnswerEndpointsTests.SendAsync(client, HttpMethod.Get, $"{Base}/status", token));
            if (condition(status))
            {
                return status;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact]
    public async Task AnAdministratorSeesTheStatus()
    {
        var (client, token) = await DocumentAnswerEndpointsTests.SignInAsync(_factory, SystemRoles.Administrator);

        var response = await DocumentAnswerEndpointsTests.SendAsync(client, HttpMethod.Get, $"{Base}/status", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await JsonOf(response);
        Assert.True(status.GetProperty("enabled").GetBoolean());
        Assert.Equal(Path.GetFullPath(_factory.DocumentsFolder), status.GetProperty("sourcePath").GetString());
        Assert.Equal("nomic-embed-text", status.GetProperty("embeddingModel").GetString());
        foreach (var field in new[] { "folderFound", "fileCount", "chunkCount", "lastIndexedAt", "isRunning", "lastError", "lastErrorFile" })
        {
            Assert.True(status.TryGetProperty(field, out _), $"missing {field}");
        }
    }

    [Theory]
    [InlineData(SystemRoles.Employee)]
    [InlineData(SystemRoles.Manager)]
    public async Task AUserWithoutTheTfaSettingsPermissionsIsRefusedBoth(string role)
    {
        var (client, token) = await DocumentAnswerEndpointsTests.SignInAsync(_factory, role);

        Assert.Equal(HttpStatusCode.Forbidden, (await DocumentAnswerEndpointsTests.SendAsync(client, HttpMethod.Get, $"{Base}/status", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await DocumentAnswerEndpointsTests.SendAsync(client, HttpMethod.Post, $"{Base}/reindex", token)).StatusCode);
    }

    [Fact]
    public async Task ReindexIndexesNowAndASecondRequestWhileRunningStartsNothingNew()
    {
        var (client, token) = await DocumentAnswerEndpointsTests.SignInAsync(_factory, SystemRoles.Administrator);
        File.WriteAllText(Path.Combine(_factory.DocumentsFolder, $"new-{Guid.NewGuid():N}.md"), "# New\n\n## Login\nThe login screen accepts a username or an email.");
        _factory.Embeddings.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var first = await DocumentAnswerEndpointsTests.SendAsync(client, HttpMethod.Post, $"{Base}/reindex", token);
            Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
            Assert.True((await JsonOf(first)).GetProperty("isRunning").GetBoolean());
            await WaitForStatusAsync(client, token, status => status.GetProperty("isRunning").GetBoolean());

            var second = await DocumentAnswerEndpointsTests.SendAsync(client, HttpMethod.Post, $"{Base}/reindex", token);
            Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
            Assert.True((await JsonOf(second)).GetProperty("isRunning").GetBoolean());
        }
        finally
        {
            _factory.Embeddings.Gate.TrySetResult();
            _factory.Embeddings.Gate = null;
        }

        var finished = await WaitForStatusAsync(client, token, status => !status.GetProperty("isRunning").GetBoolean());
        await Task.Delay(1000);
        var later = await JsonOf(await DocumentAnswerEndpointsTests.SendAsync(client, HttpMethod.Get, $"{Base}/status", token));

        Assert.True(finished.GetProperty("fileCount").GetInt32() >= 3);
        Assert.Equal(finished.GetProperty("lastIndexedAt").GetString(), later.GetProperty("lastIndexedAt").GetString());
        Assert.False(later.GetProperty("isRunning").GetBoolean());
    }
}
