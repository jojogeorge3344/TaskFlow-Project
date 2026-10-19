using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.UnitTests;

/// <summary>Runs the real migrations against a file database: the upgrade from the old Role column, the triggers that
/// lock the system roles and the catalog, and the startup reconciler.</summary>
public class RolesMigrationTests : IDisposable
{
    private const string BeforeRoles = "20261018183210_AddAssistant";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"taskflow-migration-{Guid.NewGuid():N}.db");

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

    private async System.Threading.Tasks.Task<AppDbContext> MigratedAndSeededAsync()
    {
        var db = NewContext();
        await db.Database.MigrateAsync();
        await new AccessControlSeeder(db, TimeProvider.System).SeedAsync();
        return db;
    }

    [Fact]
    public async System.Threading.Tasks.Task Upgrade_MovesEachOldRoleOntoItsSystemRoleAndEndsSessions()
    {
        await using (var old = NewContext())
        {
            await old.GetService<IMigrator>().MigrateAsync(BeforeRoles);
            foreach (var (name, role) in new[] { ("old-admin", "Administrator"), ("old-manager", "Manager"), ("old-employee", "Employee") })
            {
                await old.Database.ExecuteSqlRawAsync(
                    "INSERT INTO \"Users\" (\"FullName\", \"Username\", \"Email\", \"PasswordHash\", \"SecurityStamp\", \"Role\", \"IsActive\", \"CreatedAt\", \"UpdatedAt\", \"IsDeleted\") "
                    + "VALUES ({0}, {0}, {1}, 'hash', 'stamp-before', {2}, 1, '2026-10-01', '2026-10-01', 0)",
                    name,
                    $"{name}@taskflow.local",
                    role);
            }
        }

        await using var db = NewContext();
        await db.Database.MigrateAsync();

        var users = await db.Users.AsNoTracking().OrderBy(user => user.Id).ToListAsync();

        Assert.Equal(
            [SystemRoles.AdministratorId, SystemRoles.ManagerId, SystemRoles.EmployeeId],
            users.Select(user => user.RoleId));
        Assert.All(users, user => Assert.NotEqual("stamp-before", user.SecurityStamp));
        Assert.Equal(3, users.Select(user => user.SecurityStamp).Distinct().Count());
        Assert.Equal(
            [SystemRoles.Administrator, SystemRoles.Manager, SystemRoles.Employee],
            await db.Roles.OrderBy(role => role.Id).Select(role => role.Name).ToListAsync());
        Assert.All(await db.Roles.ToListAsync(), role => Assert.True(role.IsSystem));
    }

    [Fact]
    public async System.Threading.Tasks.Task SystemRoleRowsRejectUpdatesAndDeletes()
    {
        await using var db = await MigratedAndSeededAsync();

        var rename = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE \"Roles\" SET \"Name\" = 'Boss' WHERE \"Id\" = 1"));
        var delete = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM \"Roles\" WHERE \"Id\" = 2"));

        Assert.Contains("System roles are read-only", rename.ToString());
        Assert.Contains("System roles cannot be deleted", delete.ToString());
        Assert.Equal(SystemRoles.Administrator, await db.Roles.AsNoTracking().Where(role => role.Id == 1).Select(role => role.Name).SingleAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task SystemRoleGrantsRejectUpdatesAndDeletes()
    {
        await using var db = await MigratedAndSeededAsync();
        var before = await db.RolePermissions.CountAsync(grant => grant.RoleId == SystemRoles.ManagerId);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM \"RolePermissions\" WHERE \"RoleId\" = 2"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE \"RolePermissions\" SET \"PermissionId\" = 1 WHERE \"RoleId\" = 3 OR \"RoleId\" = 2"));

        Assert.True(before > 0);
        Assert.Equal(before, await db.RolePermissions.CountAsync(grant => grant.RoleId == SystemRoles.ManagerId));
    }

    [Fact]
    public async System.Threading.Tasks.Task PermissionCatalogRowsRejectUpdatesAndDeletes()
    {
        await using var db = await MigratedAndSeededAsync();
        var count = await db.Permissions.CountAsync();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE \"Permissions\" SET \"Name\" = 'Changed'"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM \"Permissions\" WHERE \"Key\" = 'tasks.create'"));

        Assert.Equal(PermissionCatalog.All.Count, count);
        Assert.Equal(count, await db.Permissions.CountAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task InsertsAndCustomRoleChangesAreStillAllowed()
    {
        await using var db = await MigratedAndSeededAsync();
        var tasksCreate = await db.Permissions.SingleAsync(permission => permission.Key == PermissionKeys.TasksCreate);

        var custom = new Role { Name = "Team Lead", IsSystem = false, CreatedAt = DateTime.UtcNow };
        custom.Permissions.Add(new RolePermission { Permission = tasksCreate });
        db.Roles.Add(custom);
        await db.SaveChangesAsync();

        custom.Name = "Team Leader";
        await db.SaveChangesAsync();
        db.Roles.Remove(custom);
        await db.SaveChangesAsync();

        Assert.False(await db.Roles.AnyAsync(role => role.Name.StartsWith("Team Lead")));
        Assert.False(await db.RolePermissions.AnyAsync(grant => grant.RoleId == custom.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task RoleNamesAreUniqueIgnoringCase()
    {
        await using var db = await MigratedAndSeededAsync();

        db.Roles.Add(new Role { Name = "manager", IsSystem = false, CreatedAt = DateTime.UtcNow });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task Seeder_GrantsTheAdministratorANewCatalogEntryAndLeavesOtherGrantsAlone()
    {
        await using var db = await MigratedAndSeededAsync();

        // Pretend tasks.reassign is a catalog entry added by a later release: it is not in the database yet.
        // The migrated database refuses deletes, so the triggers are dropped just for this simulation.
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER \"TR_RolePermissions_SystemNoDelete\"");
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER \"TR_Permissions_NoDelete\"");
        var managerGrantsBefore = await db.RolePermissions
            .Where(grant => grant.RoleId == SystemRoles.ManagerId && grant.Permission.Key != PermissionKeys.TasksReassign)
            .Select(grant => grant.Permission.Key)
            .OrderBy(key => key)
            .ToListAsync();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"RolePermissions\" WHERE \"PermissionId\" = (SELECT \"Id\" FROM \"Permissions\" WHERE \"Key\" = 'tasks.reassign')");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Permissions\" WHERE \"Key\" = 'tasks.reassign'");
        // An administrator-made change to the Manager that startup must never undo.
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"RolePermissions\" WHERE \"RoleId\" = 2 AND \"PermissionId\" = (SELECT \"Id\" FROM \"Permissions\" WHERE \"Key\" = 'reports.view')");

        await new AccessControlSeeder(db, TimeProvider.System).SeedAsync();

        var adminKeys = await db.RolePermissions
            .Where(grant => grant.RoleId == SystemRoles.AdministratorId)
            .Select(grant => grant.Permission.Key)
            .ToListAsync();
        var managerKeys = await db.RolePermissions
            .Where(grant => grant.RoleId == SystemRoles.ManagerId)
            .Select(grant => grant.Permission.Key)
            .OrderBy(key => key)
            .ToListAsync();

        Assert.Equal(PermissionCatalog.All.Select(permission => permission.Key).Order(), adminKeys.Order());
        Assert.Contains(PermissionKeys.TasksReassign, managerKeys);
        Assert.DoesNotContain(PermissionKeys.ReportsView, managerKeys);
        Assert.Equal(
            managerGrantsBefore.Where(key => key != PermissionKeys.ReportsView),
            managerKeys.Where(key => key != PermissionKeys.TasksReassign));
    }

    [Fact]
    public async System.Threading.Tasks.Task Seeder_GivesTheManagerAndEmployeeTheirDefaultGrantsAndTheAdministratorEverything()
    {
        await using var db = await MigratedAndSeededAsync();

        async System.Threading.Tasks.Task<List<string>> KeysFor(int roleId) => await db.RolePermissions
            .Where(grant => grant.RoleId == roleId)
            .Select(grant => grant.Permission.Key)
            .OrderBy(key => key)
            .ToListAsync();

        Assert.Equal(PermissionCatalog.All.Select(permission => permission.Key).Order(), await KeysFor(SystemRoles.AdministratorId));
        Assert.Equal(
            PermissionCatalog.All.Where(permission => permission.GrantedToManager).Select(permission => permission.Key).Order(),
            await KeysFor(SystemRoles.ManagerId));
        Assert.Equal(
            PermissionCatalog.All.Where(permission => permission.GrantedToEmployee).Select(permission => permission.Key).Order(),
            await KeysFor(SystemRoles.EmployeeId));
        Assert.DoesNotContain(PermissionKeys.RolesManage, await KeysFor(SystemRoles.ManagerId));
        Assert.False(await db.Permissions.Where(permission => permission.Key == PermissionKeys.RolesManage).Select(permission => permission.IsAssignable).SingleAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task Seeder_IsIdempotent()
    {
        await using var db = await MigratedAndSeededAsync();
        var permissions = await db.Permissions.CountAsync();
        var grants = await db.RolePermissions.CountAsync();

        await new AccessControlSeeder(db, TimeProvider.System).SeedAsync();
        await new AccessControlSeeder(db, TimeProvider.System).SeedAsync();

        Assert.Equal(permissions, await db.Permissions.CountAsync());
        Assert.Equal(grants, await db.RolePermissions.CountAsync());
        Assert.Equal(3, await db.Roles.CountAsync());
    }
}
