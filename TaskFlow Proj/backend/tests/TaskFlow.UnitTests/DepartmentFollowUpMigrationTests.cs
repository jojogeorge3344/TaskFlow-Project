using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Persistence.Seeders;

namespace TaskFlow.UnitTests;

/// <summary>Runs the real migrations that follow the Department/Project split: roles keep their project access and a
/// deleted department or project no longer reserves its name.</summary>
public class DepartmentFollowUpMigrationTests : IDisposable
{
    private const string BeforeFollowUp = "20261020173455_RenameDepartmentsToProjectsAndAddDepartments";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"taskflow-followup-{Guid.NewGuid():N}.db");

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

    [Fact]
    public async System.Threading.Tasks.Task Upgrade_GivesRolesThatHeldTheOldGrantsTheProjectKeysToo()
    {
        await using (var old = NewContext())
        {
            await old.GetService<IMigrator>().MigrateAsync(BeforeFollowUp);

            // A database that was seeded before the split: the old keys exist and a custom role holds them.
            await old.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Permissions\" (\"Key\", \"Name\", \"Description\", \"Group\", \"IsAssignable\") VALUES "
                + "('departments.view', 'View departments', 'd', 'Departments', 1), "
                + "('departments.manage', 'Manage departments', 'd', 'Departments', 1), "
                + "('tasks.view', 'View tasks', 'd', 'Tasks', 1)");
            await old.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Roles\" (\"Name\", \"IsSystem\", \"CreatedAt\") VALUES ('Project lead', 0, '2026-10-01'), ('Viewer', 0, '2026-10-01')");
            await old.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"RolePermissions\" (\"RoleId\", \"PermissionId\") "
                + "SELECT r.\"Id\", p.\"Id\" FROM \"Roles\" r, \"Permissions\" p "
                + "WHERE r.\"Name\" = 'Project lead' AND p.\"Key\" IN ('departments.view', 'departments.manage')");
            await old.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"RolePermissions\" (\"RoleId\", \"PermissionId\") "
                + "SELECT r.\"Id\", p.\"Id\" FROM \"Roles\" r, \"Permissions\" p "
                + "WHERE r.\"Name\" = 'Viewer' AND p.\"Key\" = 'tasks.view'");
        }

        await using var db = NewContext();
        await db.Database.MigrateAsync();

        var keysByRole = (await db.RolePermissions
                .AsNoTracking()
                .Where(grant => !grant.Role.IsSystem)
                .Select(grant => new { grant.Role.Name, grant.Permission.Key })
                .ToListAsync())
            .GroupBy(grant => grant.Name)
            .ToDictionary(group => group.Key, group => group.Select(grant => grant.Key).Order().ToList());

        Assert.Equal(
            ["departments.manage", "departments.view", "projects.manage", "projects.view"],
            keysByRole["Project lead"]);
        Assert.Equal(["tasks.view"], keysByRole["Viewer"]);
    }

    [Fact]
    public async System.Threading.Tasks.Task Upgrade_LeavesAFreshDatabaseToTheSeederSoManagersStillGetTheirDefaults()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        Assert.Equal(0, await ScalarAsync(db, "SELECT COUNT(*) FROM \"Permissions\" WHERE \"Key\" LIKE 'projects.%'"));

        await new AccessControlSeeder(db, TimeProvider.System).SeedAsync();

        var managerKeys = await db.RolePermissions
            .AsNoTracking()
            .Where(grant => grant.RoleId == SystemRoles.ManagerId)
            .Select(grant => grant.Permission.Key)
            .ToListAsync();
        Assert.Contains(PermissionKeys.ProjectsView, managerKeys);
    }

    [Theory]
    [InlineData("Departments")]
    [InlineData("Projects")]
    public async System.Threading.Tasks.Task ADeletedRowNoLongerReservesItsNameButTwoLiveRowsStillClash(string table)
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        var insertCreatedAt = "'2026-10-01'";
        await db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO \"{table}\" (\"Name\", \"CreatedAt\", \"IsDeleted\") VALUES ('Alpha', {insertCreatedAt}, 1)");
        await db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO \"{table}\" (\"Name\", \"CreatedAt\", \"IsDeleted\") VALUES ('Alpha', {insertCreatedAt}, 0)");

        var duplicate = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO \"{table}\" (\"Name\", \"CreatedAt\", \"IsDeleted\") VALUES ('Alpha', {insertCreatedAt}, 0)"));

        Assert.Contains("UNIQUE", duplicate.ToString());
        Assert.Equal(2, await ScalarAsync(db, $"SELECT COUNT(*) FROM \"{table}\" WHERE \"Name\" = 'Alpha'"));
    }
}
