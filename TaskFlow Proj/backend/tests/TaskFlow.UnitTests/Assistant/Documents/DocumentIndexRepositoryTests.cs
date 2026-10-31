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
        db.Database.EnsureCreated();
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
}
