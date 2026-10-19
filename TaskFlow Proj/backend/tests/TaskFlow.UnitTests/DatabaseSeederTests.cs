using Microsoft.AspNetCore.Identity;
using TaskFlow.Domain.Security;
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
    public async System.Threading.Tasks.Task SeedAsync_OnCleanDatabase_CreatesTheAdministratorManagerAndEmployee()
    {
        await using var harness = SeederHarness.Create();

        await harness.Seeder.SeedAsync();

        var users = await harness.Db.Users.OrderBy(user => user.Id).ToListAsync();

        Assert.Equal(
            [("admin", "admin@taskflow.local", SystemRoles.AdministratorId),
             ("manager", "manager@taskflow.local", SystemRoles.ManagerId),
             ("employee", "employee@taskflow.local", SystemRoles.EmployeeId)],
            users.Select(user => (user.Username, user.Email, user.RoleId)));
        Assert.All(users, user => Assert.True(user.IsActive));
        Assert.All(users, user => Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash)));
    }

    [Fact]
    public async System.Threading.Tasks.Task SeedAsync_WhenRunTwice_IsIdempotent()
    {
        await using var harness = SeederHarness.Create();

        await harness.Seeder.SeedAsync();
        await harness.Seeder.SeedAsync();

        Assert.Equal(3, await harness.Db.Users.CountAsync());
    }

    [Theory]
    [InlineData("admin", "123456")]
    [InlineData("manager", "123456")]
    [InlineData("employee", "123456")]
    public async System.Threading.Tasks.Task SeedAsync_StoresOnlyAHashThatVerifiesAgainstThePassword(string username, string password)
    {
        await using var harness = SeederHarness.Create();

        await harness.Seeder.SeedAsync();

        var user = await harness.Db.Users.SingleAsync(candidate => candidate.Username == username);
        var verification = new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, password);

        Assert.NotEqual(PasswordVerificationResult.Failed, verification);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.DoesNotContain(password, user.PasswordHash);
    }

    [Fact]
    public async System.Threading.Tasks.Task SeedAsync_LeavesAnExistingAccountUnchangedAndAddsOnlyTheMissingOnes()
    {
        await using var harness = SeederHarness.Create();
        await harness.Seeder.SeedAsync();

        var manager = await harness.Db.Users.SingleAsync(user => user.Username == "manager");
        var changedHash = new PasswordHasher<User>().HashPassword(manager, "Changed@999");
        manager.PasswordHash = changedHash;
        manager.RoleId = SystemRoles.EmployeeId;
        var employee = await harness.Db.Users.SingleAsync(user => user.Username == "employee");
        harness.Db.Users.Remove(employee);
        await harness.Db.SaveChangesAsync();

        await harness.Seeder.SeedAsync();

        var reloaded = await harness.Db.Users.AsNoTracking().SingleAsync(user => user.Username == "manager");
        Assert.Equal(changedHash, reloaded.PasswordHash);
        Assert.Equal(SystemRoles.EmployeeId, reloaded.RoleId);
        Assert.True(await harness.Db.Users.AnyAsync(user => user.Username == "employee"));
        Assert.Equal(3, await harness.Db.Users.CountAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task SeedAsync_WhenDisabled_CreatesNoAccounts()
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
            TestAccessControl.Seed(db);

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
