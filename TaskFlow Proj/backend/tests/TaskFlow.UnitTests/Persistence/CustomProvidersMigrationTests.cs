using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

namespace TaskFlow.UnitTests.Persistence;

/// <summary>Runs the real AddCustomAiProviders migration over a database saved before providers became data: the three
/// provider rows (one with an encrypted key), administrator-added models and chat messages must come through unchanged,
/// pointing at the built-in providers by id.</summary>
public class CustomProvidersMigrationTests : IDisposable
{
    private const string Before = "20261031134802_AddDocumentIndex";
    private const string This = "20261101050239_AddCustomAiProviders";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"taskflow-providers-migration-{Guid.NewGuid():N}.db");

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

    /// <summary>A database at the migration before this one, holding what an installation had saved.</summary>
    private async System.Threading.Tasks.Task SeedOldDatabaseAsync()
    {
        await using var old = NewContext();
        await old.GetService<IMigrator>().MigrateAsync(Before);
        await AddUserAsync(old);

        await old.Database.ExecuteSqlRawAsync("""
            INSERT INTO "AiProviderConfigs" ("Kind", "IsEnabled", "ModelName", "BaseUrl", "EncryptedApiKey", "KeyLast4", "UpdatedAt") VALUES
                ('Ollama', 0, 'llama3.2:3b', 'http://gpu-box:11434', NULL, NULL, '2026-10-30 10:00:00'),
                ('Gemini', 1, 'gemini-2.5-pro', NULL, 'protected-gemini-key', '1234', '2026-10-30 10:00:00'),
                ('OpenAI', 0, 'gpt-4o-mini', NULL, NULL, NULL, '2026-10-30 10:00:00');
            INSERT INTO "AiModels" ("Kind", "Name", "IsBuiltIn") VALUES
                ('Ollama', 'llama3.2:3b', 1),
                ('Gemini', 'gemini-3.8-flash', 1),
                ('Gemini', 'gemini-2.5-pro', 0),
                ('OpenAI', 'gpt-4o-mini', 1);
            INSERT INTO "AssistantConversations" ("UserId", "CreatedAt", "UpdatedAt")
                VALUES ((SELECT "Id" FROM "Users" WHERE "Username" = 'asha'), '2026-10-30', '2026-10-30');
            INSERT INTO "AssistantMessages" ("ConversationId", "Role", "Content", "ProviderKind", "CreatedAt") VALUES
                ((SELECT MAX("Id") FROM "AssistantConversations"), 'User', 'hello', NULL, '2026-10-30 10:00:00'),
                ((SELECT MAX("Id") FROM "AssistantConversations"), 'Assistant', 'hi from gemini', 'Gemini', '2026-10-30 10:00:01'),
                ((SELECT MAX("Id") FROM "AssistantConversations"), 'Assistant', 'hi from ollama', 'Ollama', '2026-10-30 10:00:02');
            """);
    }

    /// <summary>Adds a user with every required column of the old Users table filled (whatever that table held then).</summary>
    private static async System.Threading.Tasks.Task AddUserAsync(AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        var columns = new List<(string Name, string Type)>();
        await using (var info = connection.CreateCommand())
        {
            info.CommandText = "SELECT \"name\", \"type\" FROM pragma_table_info('Users') WHERE \"notnull\" = 1 AND \"dflt_value\" IS NULL AND \"pk\" = 0";
            await using var reader = await info.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        var known = new Dictionary<string, string>
        {
            ["FullName"] = "'Asha'", ["Username"] = "'asha'", ["Email"] = "'asha@taskflow.com'", ["RoleId"] = "3", ["IsActive"] = "1"
        };
        var values = columns.Select(column => known.TryGetValue(column.Name, out var value)
            ? value
            : column.Type.StartsWith("INTEGER", StringComparison.OrdinalIgnoreCase) ? "0" : "'2026-10-01'");
        await using var insert = connection.CreateCommand();
        insert.CommandText = $"INSERT INTO \"Users\" ({string.Join(", ", columns.Select(column => $"\"{column.Name}\""))}) VALUES ({string.Join(", ", values)})";
        await insert.ExecuteNonQueryAsync();
    }

    [Fact]
    public async System.Threading.Tasks.Task ProvidersModelsAndMessagesComeThroughOnTheBuiltInProviders()
    {
        await SeedOldDatabaseAsync();

        await using var db = NewContext();
        await db.Database.MigrateAsync();

        var providers = await db.AiProviderConfigs.AsNoTracking().ToDictionaryAsync(row => row.ApiType);
        Assert.Equal(3, providers.Count);
        Assert.All(providers.Values, row => Assert.True(row.IsBuiltIn));

        var gemini = providers[AiApiType.Gemini];
        Assert.Equal("Google Gemini", gemini.Name);
        Assert.Equal("Cloud", gemini.Locality);
        Assert.True(gemini.IsEnabled);
        Assert.Equal("gemini-2.5-pro", gemini.ModelName);
        Assert.Equal("protected-gemini-key", gemini.EncryptedApiKey);
        Assert.Equal("1234", gemini.KeyLast4);

        var ollama = providers[AiApiType.Ollama];
        Assert.Equal("Ollama", ollama.Name);
        Assert.Equal("Local", ollama.Locality);
        Assert.Equal("http://gpu-box:11434", ollama.BaseUrl);
        Assert.Equal("OpenAI GPT", providers[AiApiType.OpenAI].Name);

        var models = await db.AiModels.AsNoTracking().ToListAsync();
        Assert.Equal(4, models.Count);
        var added = models.Single(model => model.Name == "gemini-2.5-pro");
        Assert.Equal(gemini.Id, added.ProviderId);
        Assert.False(added.IsBuiltIn);
        Assert.Equal(ollama.Id, models.Single(model => model.Name == "llama3.2:3b").ProviderId);

        var messages = await db.AssistantMessages.AsNoTracking().OrderBy(message => message.CreatedAt).Select(message => message.ProviderId).ToListAsync();
        Assert.Equal([null, gemini.Id, ollama.Id], messages);
    }

    [Fact]
    public async System.Threading.Tasks.Task ACustomProviderCanBeAddedAfterTheUpgradeAndNamesIgnoreCase()
    {
        await SeedOldDatabaseAsync();
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        db.AiProviderConfigs.Add(new AiProviderConfig
        {
            Name = "Groq", ApiType = AiApiType.OpenAI, Locality = "Cloud", ModelName = "llama-3.3-70b-versatile",
            BaseUrl = "https://api.groq.com/openai/v1", UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        db.AiProviderConfigs.Add(new AiProviderConfig
        {
            Name = "groq", ApiType = AiApiType.OpenAI, Locality = "Cloud", ModelName = "x", BaseUrl = "https://example.com/v1", UpdatedAt = DateTime.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task DownThenUpAgainKeepsTheBuiltInProviders()
    {
        await SeedOldDatabaseAsync();
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
            await db.GetService<IMigrator>().MigrateAsync(Before);
        }

        await using (var down = NewContext())
        {
            var kinds = await down.Database.SqlQueryRaw<string>("SELECT \"Kind\" AS \"Value\" FROM \"AiModels\" WHERE \"Name\" = 'gemini-2.5-pro'").ToListAsync();
            Assert.Equal(["Gemini"], kinds);
        }

        await using var again = NewContext();
        await again.GetService<IMigrator>().MigrateAsync(This);
        Assert.Equal(3, await again.AiProviderConfigs.CountAsync(row => row.IsBuiltIn));
        Assert.Equal(4, await again.AiModels.CountAsync());
    }
}
