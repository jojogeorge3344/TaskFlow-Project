using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskFlow.Api.Persistence;

namespace TaskFlow.UnitTests.Persistence;

/// <summary>Runs the real migration that adds the TFA document index, on a database that already holds data.</summary>
public class DocumentIndexMigrationTests : IDisposable
{
    private const string Before = "20261024122725_AddTaskCommentEditDelete";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"taskflow-documents-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_path};Default Timeout=30").Options);

    private static async System.Threading.Tasks.Task<Dictionary<string, string>> SchemaAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT \"name\", \"sql\" FROM sqlite_master WHERE \"sql\" IS NOT NULL AND \"name\" NOT LIKE '__EFMigrations%'";
        var schema = new Dictionary<string, string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            schema[reader.GetString(0)] = reader.GetString(1);
        }

        return schema;
    }

    [Fact]
    public async System.Threading.Tasks.Task Upgrade_AddsTheDocumentTablesAndLeavesEveryExistingTableAndRowAlone()
    {
        Dictionary<string, string> before;
        await using (var old = NewContext())
        {
            await old.GetService<IMigrator>().MigrateAsync(Before);
            await old.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Projects\" (\"Id\", \"Name\", \"CreatedAt\", \"IsDeleted\") VALUES (1, 'Apollo', '2026-10-01', 0)");
            before = await SchemaAsync(old);
        }

        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var after = await SchemaAsync(db);

        Assert.All(before, entry => Assert.Equal(entry.Value, after[entry.Key]));
        Assert.Equal(
            ["DocumentChunks", "DocumentSources", "IX_DocumentChunks_DocumentSourceId", "IX_DocumentSources_RelativePath"],
            after.Keys.Except(before.Keys).Order());
        Assert.Equal("Apollo", (await db.Projects.AsNoTracking().SingleAsync()).Name);
    }

    [Fact]
    public async System.Threading.Tasks.Task AFreshDatabaseGetsTheDocumentTablesAndDeletingASourceDeletesItsChunks()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        var source = new TaskFlow.Api.Models.Entities.DocumentSource
        {
            RelativePath = "TF - FRD/a.md",
            Title = "A",
            ContentHash = new string('0', 64),
            EmbeddingModel = "nomic-embed-text",
            ChunkCount = 1,
            IndexedAt = DateTime.UtcNow,
            Chunks = [new() { Ordinal = 0, HeadingPath = "A", Text = "text", Embedding = [0, 0, 128, 63], Dimensions = 1 }]
        };
        db.DocumentSources.Add(source);
        await db.SaveChangesAsync();

        db.DocumentSources.Remove(source);
        await db.SaveChangesAsync();

        Assert.Equal(0, await db.DocumentChunks.CountAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task TwoSourcesCannotShareAPath()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        db.DocumentSources.AddRange(
            new() { RelativePath = "a.md", Title = "A", ContentHash = "x", EmbeddingModel = "m", IndexedAt = DateTime.UtcNow },
            new() { RelativePath = "a.md", Title = "B", ContentHash = "y", EmbeddingModel = "m", IndexedAt = DateTime.UtcNow });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task Downgrade_RemovesOnlyTheDocumentTables()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var upgraded = await SchemaAsync(db);

        await db.GetService<IMigrator>().MigrateAsync(Before);
        var downgraded = await SchemaAsync(db);

        Assert.Equal(upgraded.Keys.Where(name => !name.Contains("Document")).Order(), downgraded.Keys.Order());
    }
}
