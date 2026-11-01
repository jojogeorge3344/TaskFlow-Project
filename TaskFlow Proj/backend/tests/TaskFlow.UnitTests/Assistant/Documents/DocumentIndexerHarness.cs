using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Config;
using TaskFlow.Api.Integrations.Ai.Embeddings;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Assistant.Documents;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;
using TaskFlow.Api.Services.Assistant.Documents;

namespace TaskFlow.UnitTests.Assistant.Documents;

/// <summary>Embeds text as a small vector derived from its words, and records every call. It can be told to fail.</summary>
internal sealed class FakeEmbeddingClient : IEmbeddingClient
{
    public List<(EmbeddingEndpoint Endpoint, IReadOnlyList<string> Inputs, EmbeddingPurpose Purpose)> Calls { get; } = [];

    public Func<string, Exception?> FailFor { get; set; } = _ => null;

    public IEnumerable<string> EmbeddedDocuments => Calls.Where(call => call.Purpose == EmbeddingPurpose.Document).SelectMany(call => call.Inputs);

    /// <summary>When set, every call waits for it, so a test can hold an indexing run in progress.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<float[][]> EmbedAsync(EmbeddingEndpoint endpoint, IReadOnlyList<string> inputs, EmbeddingPurpose purpose, CancellationToken cancellationToken = default)
    {
        Entered.TrySetResult();
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        foreach (var input in inputs)
        {
            if (FailFor(input) is { } failure)
            {
                throw failure;
            }
        }

        lock (Calls)
        {
            Calls.Add((endpoint, inputs, purpose));
        }

        return inputs.Select(Vector).ToArray();
    }

    /// <summary>One dimension per keyword, so texts that share a keyword are similar.</summary>
    public static float[] Vector(string text)
    {
        string[] keywords = ["task", "reminder", "mail", "login", "report"];
        var vector = keywords.Select(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase) ? 1f : 0f).Append(0.1f).ToArray();
        var length = (float)Math.Sqrt(vector.Sum(value => value * value));
        return vector.Select(value => value / length).ToArray();
    }
}

internal sealed class DocumentIndexerHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public DocumentIndexerHarness()
    {
        Directory.CreateDirectory(Folder);
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
        db.AiProviderConfigs.Add(new TaskFlow.Api.Models.Entities.AiProviderConfig
        {
            Name = "Google Gemini", Locality = "Cloud", IsBuiltIn = true, ApiType = TaskFlow.Api.Models.Enums.AiApiType.Gemini,
            ModelName = "gemini-3.8-flash", EncryptedApiKey = FakeKeyProtector.Encrypted(GeminiKey), UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    public const string GeminiKey = "test-gemini-key";

    /// <summary>Replaces the saved Gemini key: null removes it, a value without the fake prefix cannot be decrypted.</summary>
    public async Task SetSavedGeminiKeyAsync(string? encryptedKey)
    {
        await using var db = NewContext();
        var row = await db.AiProviderConfigs.SingleAsync(config => config.ApiType == TaskFlow.Api.Models.Enums.AiApiType.Gemini);
        row.EncryptedApiKey = encryptedKey;
        await db.SaveChangesAsync();
    }

    public string Folder { get; } = Path.Combine(Path.GetTempPath(), $"taskflow-docs-{Guid.NewGuid():N}");

    public DocumentKnowledgeOptions Options { get; } = new();

    public FakeEmbeddingClient Embeddings { get; } = new();

    public DocumentVectorIndex Index { get; } = new();

    public DocumentIndexStatus Status { get; } = new();

    public AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Write(string relativePath, string content)
    {
        var path = Path.Combine(Folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Delete(string relativePath) => File.Delete(Path.Combine(Folder, relativePath));

    /// <summary>A fresh indexer over a fresh context, like a new scope of the background service.</summary>
    public async Task<DocumentIndexRun> RunAsync()
    {
        Options.SourcePath = Folder;
        await using var db = NewContext();
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        var indexer = new DocumentIndexer(
            new DocumentIndexRepository(db),
            new UnitOfWork(db),
            Embeddings,
            new DocumentEmbeddingEndpoint(new AiProviderConfigRepository(db), new FakeKeyProtector(), options),
            Index,
            Status,
            options,
            new HostingEnvironment { ContentRootPath = Path.GetTempPath() },
            TimeProvider.System,
            NullLogger<DocumentIndexer>.Instance);
        return await indexer.RunAsync();
    }

    public async Task<List<DocumentChunkRow>> StoredChunksAsync()
    {
        await using var db = NewContext();
        var rows = new List<DocumentChunkRow>();
        await foreach (var row in new DocumentIndexRepository(db).StreamChunksAsync())
        {
            rows.Add(row);
        }

        return rows;
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }
}
