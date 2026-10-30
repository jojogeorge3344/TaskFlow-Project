using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskFlow.Api.Persistence;

namespace TaskFlow.UnitTests;

/// <summary>Runs the real migration that replaces the single project, department and manager columns on a user with project
/// allocations and department memberships, on data that was written before it.</summary>
public class UserProjectAllocationsMigrationTests : IDisposable
{
    private const string Before = "20261023101938_AddMfaAndAccountRecovery";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"taskflow-allocations-{Guid.NewGuid():N}.db");

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

    private static async System.Threading.Tasks.Task<int> ScalarAsync(AppDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async System.Threading.Tasks.Task SeedOldShapeAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Projects\" (\"Id\", \"Name\", \"CreatedAt\", \"IsDeleted\") VALUES (1, 'Apollo', '2026-10-01', 0), (2, 'Zephyr', '2026-10-01', 1)");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Departments\" (\"Id\", \"Name\", \"CreatedAt\", \"IsDeleted\") VALUES (1, 'Engineering', '2026-10-01', 0)");

        // Ada has a project and a department, Bob nothing, Cy was deleted but still held the deleted project, Di has a manager.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Users\" (\"Id\", \"FullName\", \"Username\", \"Email\", \"PasswordHash\", \"SecurityStamp\", \"RoleId\", \"IsActive\", "
            + "\"CreatedAt\", \"UpdatedAt\", \"IsDeleted\", \"ProjectId\", \"DepartmentId\", \"ManagerId\", \"MfaFailedAttempts\", \"MfaLastUsedStep\") VALUES "
            + "(1, 'Ada', 'ada', 'ada@taskflow.com', 'h', 's', 1, 1, '2026-10-01', '2026-10-01', 0, 1, 1, NULL, 0, 0), "
            + "(2, 'Bob', 'bob', 'bob@taskflow.com', 'h', 's', 1, 1, '2026-10-01', '2026-10-01', 0, NULL, NULL, NULL, 0, 0), "
            + "(3, 'Cy', 'cy', 'cy@taskflow.com', 'h', 's', 1, 0, '2026-10-01', '2026-10-01', 1, 2, NULL, NULL, 0, 0), "
            + "(4, 'Di', 'di', 'di@taskflow.com', 'h', 's', 1, 1, '2026-10-01', '2026-10-01', 0, 1, NULL, 1, 0, 0)");
    }

    [Fact]
    public async System.Threading.Tasks.Task Upgrade_TurnsEachSingleProjectIntoAHundredPercentMemberAllocationAndEachDepartmentIntoAMembership()
    {
        await using (var old = NewContext())
        {
            await old.GetService<IMigrator>().MigrateAsync(Before);
            await SeedOldShapeAsync(old);
        }

        await using var db = NewContext();
        await db.Database.MigrateAsync();

        var allocations = await db.UserProjectAllocations.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(a => a.UserId).Select(a => new { a.UserId, a.ProjectId, a.AllocationPercent, Position = a.Position.Name }).ToListAsync();
        Assert.Equal(
            [(1, 1, 100, "Member"), (3, 2, 100, "Member"), (4, 1, 100, "Member")],
            allocations.Select(a => (a.UserId, a.ProjectId, a.AllocationPercent, a.Position)));

        var memberships = await db.UserDepartments.IgnoreQueryFilters().AsNoTracking()
            .Select(m => new { m.UserId, m.DepartmentId }).ToListAsync();
        Assert.Equal((1, 1), (Assert.Single(memberships).UserId, memberships[0].DepartmentId));

        Assert.Equal(0, await ScalarAsync(db, "SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name IN ('ProjectId', 'DepartmentId', 'ManagerId')"));
        Assert.Equal(4, await ScalarAsync(db, "SELECT COUNT(*) FROM \"Users\""));
    }

    [Fact]
    public async System.Threading.Tasks.Task Upgrade_SeedsThePositionsAndRefusesAPercentageOutsideOneToOneHundred()
    {
        await using (var old = NewContext())
        {
            await old.GetService<IMigrator>().MigrateAsync(Before);
            await SeedOldShapeAsync(old);
        }

        await using var db = NewContext();
        await db.Database.MigrateAsync();

        Assert.Equal(
            ["DevOps", "Developer", "Manager", "Member", "QA", "System Admin"],
            (await db.Positions.AsNoTracking().Select(p => p.Name).ToListAsync()).Order(StringComparer.Ordinal));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"UserProjectAllocations\" (\"UserId\", \"ProjectId\", \"AllocationPercent\", \"PositionId\") VALUES (2, 1, 101, 1)"));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Positions\" (\"Name\") VALUES ('qa')"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Downgrade_RestoresTheHighestPercentageProjectAndTheFirstDepartment()
    {
        await using (var old = NewContext())
        {
            await old.GetService<IMigrator>().MigrateAsync(Before);
            await SeedOldShapeAsync(old);
        }

        await using var db = NewContext();
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"UserProjectAllocations\" (\"UserId\", \"ProjectId\", \"AllocationPercent\", \"PositionId\") VALUES (2, 1, 30, 3), (2, 2, 70, 3)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO \"UserDepartments\" (\"UserId\", \"DepartmentId\") VALUES (2, 1)");

        await db.GetService<IMigrator>().MigrateAsync(Before);

        Assert.Equal(1, await ScalarAsync(db, "SELECT \"ProjectId\" FROM \"Users\" WHERE \"Id\" = 1"));
        Assert.Equal(2, await ScalarAsync(db, "SELECT \"ProjectId\" FROM \"Users\" WHERE \"Id\" = 2"));
        Assert.Equal(1, await ScalarAsync(db, "SELECT \"DepartmentId\" FROM \"Users\" WHERE \"Id\" = 2"));
        Assert.Equal(0, await ScalarAsync(db, "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('UserProjectAllocations', 'UserDepartments', 'Positions')"));
    }
}
