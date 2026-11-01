using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;

namespace TaskFlow.UnitTests.Persistence;

/// <summary>Runs the real migration that adds the FTS5 keyword index over the TFA document chunks.</summary>
public class DocumentChunkFullTextMigrationTests : IDisposable
{
    private const string Before = "20261101050239_AddCustomAiProviders";
    private const string FullText = "20261101140000_AddDocumentChunkFullText";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"taskflow-fts-{Guid.NewGuid():N}.db");

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

    private static DocumentSource Source(string path, params string[] texts) => new()
    {
        RelativePath = path,
        Title = path,
        ContentHash = new string('0', 64),
        EmbeddingModel = "m",
        ChunkCount = texts.Length,
        IndexedAt = DateTime.UtcNow,
        Chunks = texts.Select((text, index) => new DocumentChunk
        {
            Ordinal = index, HeadingPath = $"{path} › Section {index}", Text = text, Embedding = [0, 0, 128, 63], Dimensions = 1
        }).ToList()
    };

    private static async System.Threading.Tasks.Task<long> ScalarAsync(AppDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static System.Threading.Tasks.Task<long> MatchCountAsync(AppDbContext db, string match) =>
        ScalarAsync(db, $"SELECT COUNT(*) FROM \"DocumentChunks_fts\" WHERE \"DocumentChunks_fts\" MATCH '{match}'");

    private static async System.Threading.Tasks.Task<List<string>> ObjectNamesAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT \"name\" FROM sqlite_master WHERE \"name\" NOT LIKE '__EFMigrations%' ORDER BY \"name\"";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    [Fact]
    public async System.Threading.Tasks.Task Upgrade_IndexesTheChunksAlreadyStored()
    {
        await using (var old = NewContext())
        {
            await old.GetService<IMigrator>().MigrateAsync(Before);
            old.DocumentSources.AddRange(Source("a.md", "NFR-14 reminder engine", "task validations"), Source("b.md", "dashboard"));
            await old.SaveChangesAsync();
        }

        await using var db = NewContext();
        await db.GetService<IMigrator>().MigrateAsync(FullText);

        Assert.Equal(3, await ScalarAsync(db, "SELECT COUNT(*) FROM \"DocumentChunks_fts\""));
        Assert.Equal(1, await MatchCountAsync(db, "\"NFR-14\""));
        Assert.Equal(1, await MatchCountAsync(db, "\"validation\""));
    }

    [Fact]
    public async System.Threading.Tasks.Task TheIndexFollowsInsertsUpdatesAndCascadeDeletes()
    {
        await using var db = NewContext();
        await db.GetService<IMigrator>().MigrateAsync(FullText);

        var source = Source("a.md", "alpha", "beta");
        db.DocumentSources.Add(source);
        await db.SaveChangesAsync();
        Assert.Equal(1, await MatchCountAsync(db, "\"alpha\""));

        source.Chunks[0].Text = "gamma";
        await db.SaveChangesAsync();
        Assert.Equal(0, await MatchCountAsync(db, "\"alpha\""));
        Assert.Equal(1, await MatchCountAsync(db, "\"gamma\""));

        db.DocumentSources.Remove(source);
        await db.SaveChangesAsync();
        Assert.Equal(0, await MatchCountAsync(db, "\"gamma\" OR \"beta\""));
        Assert.Equal(0, await ScalarAsync(db, "SELECT COUNT(*) FROM \"DocumentChunks_fts\""));
    }

    [Fact]
    public async System.Threading.Tasks.Task Downgrade_RemovesOnlyTheKeywordIndex()
    {
        await using var db = NewContext();
        await db.GetService<IMigrator>().MigrateAsync(Before);
        var before = await ObjectNamesAsync(db);

        await db.GetService<IMigrator>().MigrateAsync(FullText);
        Assert.Contains("DocumentChunks_fts", await ObjectNamesAsync(db));

        await db.GetService<IMigrator>().MigrateAsync(Before);
        Assert.Equal(before, await ObjectNamesAsync(db));
    }
}
