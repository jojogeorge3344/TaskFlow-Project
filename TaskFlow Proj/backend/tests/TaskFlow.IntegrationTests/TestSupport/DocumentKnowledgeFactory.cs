using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskFlow.Api.Integrations.Ai.Embeddings;

namespace TaskFlow.IntegrationTests.TestSupport;

/// <summary>Embeds text as one dimension per keyword, so a question and a section that share a keyword match. It never
/// leaves the process. A test can hold calls at <see cref="Gate"/>.</summary>
public sealed class KeywordEmbeddingClient : IEmbeddingClient
{
    private static readonly string[] Keywords = ["reminder", "task", "mail", "login", "report"];

    public TaskCompletionSource? Gate { get; set; }

    public async Task<float[][]> EmbedAsync(EmbeddingEndpoint endpoint, IReadOnlyList<string> inputs, EmbeddingPurpose purpose, CancellationToken cancellationToken = default)
    {
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        return inputs.Select(input =>
        {
            var vector = Keywords.Select(keyword => input.Contains(keyword, StringComparison.OrdinalIgnoreCase) ? 1f : 0f).Append(0.05f).ToArray();
            var length = (float)Math.Sqrt(vector.Sum(value => value * value));
            return vector.Select(value => value / length).ToArray();
        }).ToArray();
    }
}

/// <summary>The API with document indexing on, over a temporary documents folder and the keyword embedder.</summary>
public class DocumentKnowledgeFactory : TaskFlowApiFactory
{
    public string DocumentsFolder { get; } = Path.Combine(Path.GetTempPath(), $"taskflow-docs-tests-{Guid.NewGuid():N}");

    public KeywordEmbeddingClient Embeddings { get; } = new();

    public DocumentKnowledgeFactory()
    {
        Directory.CreateDirectory(Path.Combine(DocumentsFolder, "TF - FRD"));
        File.WriteAllText(
            Path.Combine(DocumentsFolder, "TF - FRD", "FRD-Reminders.md"),
            "# FRD – Reminders\n\n## 4.2 Overdue reminder\nThe overdue reminder is sent to the assignee and the task's creator, daily or weekly after the deadline.\n");
        File.WriteAllText(
            Path.Combine(DocumentsFolder, "FRD-Mail.md"),
            "# FRD – Mail\n\n## 3.1 Compose\nInternal mail is written on the Compose screen.\n");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DocumentKnowledge:Enabled"] = "true",
            ["DocumentKnowledge:SourcePath"] = DocumentsFolder,
            ["DocumentKnowledge:MinScore"] = "0.5"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmbeddingClient>();
            services.AddSingleton<IEmbeddingClient>(Embeddings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(DocumentsFolder))
        {
            Directory.Delete(DocumentsFolder, recursive: true);
        }
    }
}
