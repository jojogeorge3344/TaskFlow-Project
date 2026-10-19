using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.UnitTests;

/// <summary>Runs the real email-domain migration over a database that holds addresses on several domains.</summary>
public class EmailDomainMigrationTests : IDisposable
{
    private const string BeforeDomain = "20261019163241_AddAiModels";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"taskflow-email-migration-{Guid.NewGuid():N}.db");

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

    private static async System.Threading.Tasks.Task AddUserAsync(AppDbContext db, string username, string email, bool deleted = false) =>
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Users\" (\"FullName\", \"Username\", \"Email\", \"PasswordHash\", \"SecurityStamp\", \"RoleId\", \"IsActive\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\") "
            + "VALUES ({0}, {0}, {1}, 'hash', 'stamp', 3, 1, '2026-10-01', '2026-10-01', {2})",
            username,
            email,
            deleted ? 1 : 0);

    private async System.Threading.Tasks.Task<Dictionary<string, string>> MigrateAsync(params (string Username, string Email, bool Deleted)[] users)
    {
        await using (var old = NewContext())
        {
            await old.GetService<IMigrator>().MigrateAsync(BeforeDomain);
            foreach (var (username, email, deleted) in users)
            {
                await AddUserAsync(old, username, email, deleted);
            }
        }

        await using var db = NewContext();
        await db.Database.MigrateAsync();
        return await db.Users.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(user => user.Username, user => user.Email);
    }

    [Fact]
    public async System.Threading.Tasks.Task SeedAccountsAndOtherDomainsMoveToTheFixedDomain()
    {
        var emails = await MigrateAsync(
            ("admin", "admin@taskflow.local", false),
            ("manager", "manager@taskflow.local", false),
            ("ravi", "Ravi@gmail.com", false));

        Assert.Equal("admin@taskflow.com", emails["admin"]);
        Assert.Equal("manager@taskflow.com", emails["manager"]);
        Assert.Equal("ravi@taskflow.com", emails["ravi"]);
    }

    [Fact]
    public async System.Threading.Tasks.Task CollidingLocalPartsAreNumberedInCreationOrder()
    {
        var emails = await MigrateAsync(
            ("jo1", "jo@gmail.com", false),
            ("jo2", "jo@yahoo.com", false),
            ("jo3", "jo@taskflow.local", true));

        Assert.Equal("jo@taskflow.com", emails["jo1"]);
        Assert.Equal("jo2@taskflow.com", emails["jo2"]);
        Assert.Equal("jo3@taskflow.com", emails["jo3"]);
    }

    [Fact]
    public async System.Threading.Tasks.Task CharactersTheNewRulesRejectAreDroppedSoTheAddressStaysUsable()
    {
        var emails = await MigrateAsync(
            ("obrien", "O'Brien@gmail.com", false),
            ("tagged", "jo+tag@gmail.com", false),
            ("dots", "a..b@gmail.com", false),
            ("edges", ".lead.@gmail.com", false),
            ("spaced", "first last@gmail.com", false),
            ("symbols", "!!!@gmail.com", false),
            ("long", new string('x', 80) + "@gmail.com", false),
            // The 64-character cut lands right after the dot, which must not be left dangling.
            ("cut", new string('x', 63) + ".yy@gmail.com", false));

        Assert.Equal("obrien@taskflow.com", emails["obrien"]);
        Assert.Equal("jotag@taskflow.com", emails["tagged"]);
        Assert.Equal("a.b@taskflow.com", emails["dots"]);
        Assert.Equal("lead@taskflow.com", emails["edges"]);
        Assert.Equal("firstlast@taskflow.com", emails["spaced"]);
        Assert.StartsWith("user", emails["symbols"]);
        Assert.Equal(new string('x', 63) + "@taskflow.com", emails["cut"]);
        Assert.Equal(new string('x', 64) + "@taskflow.com", emails["long"]);
        Assert.All(emails.Values, email => Assert.True(TaskFlow.Application.Users.EmailPolicy.IsValid(email), email));
    }

    [Fact]
    public async System.Threading.Tasks.Task CleanedAddressesThatCollideAreStillNumbered()
    {
        var emails = await MigrateAsync(
            ("a", "o'brien@gmail.com", false),
            ("b", "obrien@yahoo.com", false));

        Assert.Equal("obrien@taskflow.com", emails["a"]);
        Assert.Equal("obrien2@taskflow.com", emails["b"]);
    }

    [Fact]
    public async System.Threading.Tasks.Task ANumberedAddressNeverStealsAnotherUsersOwnName()
    {
        var emails = await MigrateAsync(
            ("a", "jo@gmail.com", false),
            ("b", "jo@yahoo.com", false),
            ("c", "jo2@gmail.com", false));

        Assert.Equal(3, emails.Values.Distinct().Count());
        Assert.Equal("jo@taskflow.com", emails["a"]);
        Assert.Equal("jo2@taskflow.com", emails["c"]);
        Assert.All(emails.Values, email => Assert.EndsWith("@taskflow.com", email));
    }

    [Fact]
    public async System.Threading.Tasks.Task RunningTheMigrationSqlAgainChangesNothing()
    {
        var first = await MigrateAsync(("jo1", "jo@gmail.com", false), ("jo2", "jo@yahoo.com", false));

        await using var db = NewContext();
        var migration = new TaskFlow.Infrastructure.Migrations.NormalizeEmailDomain();
        var sql = string.Join("\n", migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Select(op => op.Sql));
        foreach (var statement in migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>())
        {
            await db.Database.ExecuteSqlRawAsync(statement.Sql);
        }

        var second = await db.Users.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(user => user.Username, user => user.Email);
        Assert.NotEmpty(sql);
        Assert.Equal(first, second);
    }
}
