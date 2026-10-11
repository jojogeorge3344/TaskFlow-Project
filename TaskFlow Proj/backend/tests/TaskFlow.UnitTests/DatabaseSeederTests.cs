using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.UnitTests;

public class DatabaseSeederTests
{
    [Fact]
    public async Task SeedAsync_OnCleanDatabase_CreatesThreeAccounts()
    {
        await using var harness = SeederHarness.Create();

        await harness.Seeder.SeedAsync();

        var users = await harness.Db.Users.OrderBy(user => user.Role).ToListAsync();

        Assert.Equal(3, users.Count);
        Assert.Contains(users, user => user.Role == Role.Administrator);
        Assert.Contains(users, user => user.Role == Role.Manager);
        Assert.Contains(users, user => user.Role == Role.Employee);
        Assert.All(users, user => Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash)));
    }

    [Fact]
    public async Task SeedAsync_WhenRunTwice_IsIdempotent()
    {
        await using var harness = SeederHarness.Create();

        await harness.Seeder.SeedAsync();
        await harness.Seeder.SeedAsync();

        Assert.Equal(3, await harness.Db.Users.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_WithPasswordsThatVerifyAgainstHashes()
    {
        await using var harness = SeederHarness.Create();

        await harness.Seeder.SeedAsync();

        var admin = await harness.Db.Users.SingleAsync(user => user.Username == "admin");
        var verification = new PasswordHasher<User>().VerifyHashedPassword(admin, admin.PasswordHash, "Admin@123");

        Assert.NotEqual(PasswordVerificationResult.Failed, verification);
    }

    [Fact]
    public async Task SeedAsync_WhenDisabled_CreatesNoAccounts()
    {
        await using var harness = SeederHarness.Create(enabled: false);

        await harness.Seeder.SeedAsync();

        Assert.Equal(0, await harness.Db.Users.CountAsync());
    }

    private sealed class SeederHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private SeederHarness(SqliteConnection connection, AppDbContext db, DatabaseSeeder seeder)
        {
            _connection = connection;
            Db = db;
            Seeder = seeder;
        }

        public AppDbContext Db { get; }

        public DatabaseSeeder Seeder { get; }

        public static SeederHarness Create(bool enabled = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new AppDbContext(options);
            db.Database.EnsureCreated();

            var seeder = new DatabaseSeeder(
                db,
                new PasswordHasher<User>(),
                Options.Create(new SeedOptions { Enabled = enabled }),
                TimeProvider.System);

            return new SeederHarness(connection, db, seeder);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
