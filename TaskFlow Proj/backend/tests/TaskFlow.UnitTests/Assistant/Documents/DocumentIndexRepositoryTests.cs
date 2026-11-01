using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Assistant.Documents;
using TaskFlow.Api.Repositories.Base;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests.Assistant.Documents;

public sealed class DocumentIndexRepositoryTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public DocumentIndexRepositoryTests()
    {
        _connection.Open();
        using var db = NewContext();
        // Migrations, not EnsureCreated: the keyword index (DocumentChunks_fts) and its triggers come from a migration.
        db.Database.Migrate();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private static DocumentSource Source(string path, string hash = "h1") =>
        new() { RelativePath = path, Title = path, ContentHash = hash, EmbeddingModel = "nomic-embed-text", IndexedAt = DateTime.UtcNow };

    private static List<DocumentChunk> Chunks(params string[] texts) =>
        texts.Select((text, index) => new DocumentChunk
        {
            Ordinal = index,
            HeadingPath = $"Heading {index}",
            Text = text,
            Embedding = BitConverter.GetBytes(1f),
            Dimensions = 1
        }).ToList();

    private async Task<List<DocumentChunkRow>> AllRowsAsync()
    {
        await using var db = NewContext();
        var rows = new List<DocumentChunkRow>();
        await foreach (var row in new DocumentIndexRepository(db).StreamChunksAsync())
        {
            rows.Add(row);
        }

        return rows;
    }

    private async Task StoreAsync(DocumentSource source, List<DocumentChunk> chunks)
    {
        await using var db = NewContext();
        var repository = new DocumentIndexRepository(db);
        var existing = await repository.FindByPathAsync(source.RelativePath) ?? source;
        existing.ContentHash = source.ContentHash;
        existing.ChunkCount = chunks.Count;
        await repository.ReplaceChunksAsync(existing, chunks);
        await new UnitOfWork(db).SaveChangesAsync();
    }

    [Fact]
    public async Task ANewSourceIsStoredWithItsChunks()
    {
        await StoreAsync(Source("a.md"), Chunks("one", "two"));

        var rows = await AllRowsAsync();
        Assert.Equal(["one", "two"], rows.Select(row => row.Text));
        Assert.All(rows, row => Assert.Equal("a.md", row.RelativePath));
        Assert.All(rows, row => Assert.Equal("nomic-embed-text", row.EmbeddingModel));
    }

    [Fact]
    public async Task ReplacingChunksSwapsOnlyThatSourcesChunks()
    {
        await StoreAsync(Source("a.md"), Chunks("old a1", "old a2"));
        await StoreAsync(Source("b.md"), Chunks("b1"));

        await StoreAsync(Source("a.md", "h2"), Chunks("new a1"));

        Assert.Equal(["new a1", "b1"], (await AllRowsAsync()).Select(row => row.Text));
        await using var db = NewContext();
        Assert.Equal("h2", (await new DocumentIndexRepository(db).FindByPathAsync("a.md"))!.ContentHash);
    }

    [Fact]
    public async Task NothingChangesUntilTheUnitOfWorkSaves()
    {
        await StoreAsync(Source("a.md"), Chunks("old"));

        await using (var db = NewContext())
        {
            var repository = new DocumentIndexRepository(db);
            await repository.ReplaceChunksAsync((await repository.FindByPathAsync("a.md"))!, Chunks("new"));
            await repository.RemoveByPathsAsync(["a.md"]);
        }

        Assert.Equal(["old"], (await AllRowsAsync()).Select(row => row.Text));
    }

    [Fact]
    public async Task RemovingASourceRemovesItsChunks()
    {
        await StoreAsync(Source("a.md"), Chunks("a"));
        await StoreAsync(Source("b.md"), Chunks("b"));

        await using (var db = NewContext())
        {
            await new DocumentIndexRepository(db).RemoveByPathsAsync(["a.md", "missing.md"]);
            await new UnitOfWork(db).SaveChangesAsync();
        }

        Assert.Equal(["b"], (await AllRowsAsync()).Select(row => row.Text));
        await using var check = NewContext();
        Assert.Equal(["b.md"], (await new DocumentIndexRepository(check).ListSourcesAsync()).Select(source => source.RelativePath));
    }

    [Fact]
    public async Task TheStreamIsOrderedByDocumentThenPosition()
    {
        await StoreAsync(Source("b.md"), Chunks("b0", "b1"));
        await StoreAsync(Source("a.md"), Chunks("a0", "a1"));

        Assert.Equal(["a0", "a1", "b0", "b1"], (await AllRowsAsync()).Select(row => row.Text));
    }

    private async Task<IReadOnlyList<int>> KeywordAsync(string ftsQuery, int limit = 20)
    {
        await using var db = NewContext();
        return await new DocumentIndexRepository(db).SearchKeywordAsync(ftsQuery, limit);
    }

    private async Task<int> ChunkIdAsync(string text)
    {
        await using var db = NewContext();
        return await db.DocumentChunks.Where(chunk => chunk.Text == text).Select(chunk => chunk.Id).SingleAsync();
    }

    [Fact]
    public async Task KeywordSearchFindsTheMatchingChunk()
    {
        await StoreAsync(Source("a.md"), Chunks("NFR-14 reminder engine runs every minute", "dashboard widgets"));

        Assert.Equal([await ChunkIdAsync("NFR-14 reminder engine runs every minute")], await KeywordAsync("\"NFR-14\""));
    }

    [Fact]
    public async Task KeywordSearchWithNoMatchIsEmpty()
    {
        await StoreAsync(Source("a.md"), Chunks("dashboard widgets"));

        Assert.Empty(await KeywordAsync("\"payroll\""));
    }

    [Fact]
    public async Task AHeadingMatchRanksAboveATextMatchAndTheLimitApplies()
    {
        var chunks = new List<DocumentChunk>
        {
            new() { Ordinal = 0, HeadingPath = "Overview", Text = "Reminders are mentioned once here among other words", Embedding = BitConverter.GetBytes(1f), Dimensions = 1 },
            new() { Ordinal = 1, HeadingPath = "Reminders", Text = "How the engine works among other words", Embedding = BitConverter.GetBytes(1f), Dimensions = 1 },
            new() { Ordinal = 2, HeadingPath = "Other", Text = "a reminder again", Embedding = BitConverter.GetBytes(1f), Dimensions = 1 }
        };
        await StoreAsync(Source("a.md"), chunks);

        var ids = await KeywordAsync("\"reminders\"", limit: 2);

        Assert.Equal(2, ids.Count);
        Assert.Equal(await ChunkIdAsync("How the engine works among other words"), ids[0]);
    }

    [Fact]
    public async Task TheKeywordIndexFollowsReplaceRemoveAndCascadeDelete()
    {
        await StoreAsync(Source("a.md"), Chunks("alpha"));
        await StoreAsync(Source("b.md"), Chunks("beta"));
        Assert.Single(await KeywordAsync("\"alpha\""));

        await StoreAsync(Source("a.md", "h2"), Chunks("gamma"));
        Assert.Empty(await KeywordAsync("\"alpha\""));
        Assert.Single(await KeywordAsync("\"gamma\""));

        await using (var db = NewContext())
        {
            await new DocumentIndexRepository(db).RemoveByPathsAsync(["b.md"]);
            await new UnitOfWork(db).SaveChangesAsync();
        }

        Assert.Empty(await KeywordAsync("\"beta\""));

        // A source deleted in SQL (not through EF) takes its chunks with it by ON DELETE CASCADE; the triggers still fire.
        await using (var db = NewContext())
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON; DELETE FROM \"DocumentSources\" WHERE \"RelativePath\" = 'a.md';");
        }

        Assert.Empty(await KeywordAsync("\"gamma\""));
        await using var check = NewContext();
        Assert.Equal(0, await check.DocumentChunks.CountAsync());
    }
}
