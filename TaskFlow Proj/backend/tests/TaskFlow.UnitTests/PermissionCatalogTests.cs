using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.UnitTests;

/// <summary>The module-based catalogue: its shape, the old keys that replaced finer ones, and that introducing the
/// finer permissions neither removes nor adds access for any existing role.</summary>
public class PermissionCatalogTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"taskflow-catalog-{Guid.NewGuid():N}.db");

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

    // ---------------------------------------------------------------- shape

    [Fact]
    public void EveryKeyIsUniqueAndEveryEntryHasAPlaceInTheHierarchy()
    {
        var keys = PermissionCatalog.All.Select(definition => definition.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(PermissionCatalog.All, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Name), definition.Key);
            Assert.False(string.IsNullOrWhiteSpace(definition.Description), definition.Key);
            Assert.False(string.IsNullOrWhiteSpace(definition.Module), definition.Key);
            Assert.False(string.IsNullOrWhiteSpace(definition.Screen), definition.Key);
            Assert.False(string.IsNullOrWhiteSpace(definition.Action), definition.Key);
        });
    }

    [Fact]
    public void ARecordRoundTripsItsHierarchyPlacement()
    {
        var definition = PermissionCatalog.All.Single(entry => entry.Key == PermissionKeys.TasksCommentCreate);

        Assert.Equal("Tasks", definition.Module);
        Assert.Equal("Task details", definition.Screen);
        Assert.Equal("Discussion", definition.Section);
        Assert.Equal("Create", definition.Action);
        Assert.Equal(definition.Module, definition.Group);
    }

    [Fact]
    public void EachModuleIsOneContiguousBlockAndNoModuleIsNamedAfterARole()
    {
        var modules = PermissionCatalog.All.Select(definition => definition.Module).ToList();
        var blocks = modules.Where((module, index) => index == 0 || modules[index - 1] != module).ToList();

        Assert.Equal(blocks.Count, blocks.Distinct().Count());
        Assert.DoesNotContain(
            PermissionCatalog.All,
            definition => new[] { "Administrator", "Admin", "Manager", "Employee" }.Any(
                role => string.Equals(definition.Module, role, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(definition.Screen, role, StringComparison.OrdinalIgnoreCase)
                    || definition.Screen.Contains(role + " dashboard", StringComparison.OrdinalIgnoreCase)
                    || definition.Section.Contains(role + " dashboard", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void DashboardIsOneModuleHoldingEveryDashboardFeature()
    {
        var dashboard = PermissionCatalog.All.Where(definition => definition.Module == PermissionCatalog.Dashboard).ToList();

        Assert.Equal(1, dashboard.Select(definition => definition.Screen).Distinct().Count());
        Assert.Contains(dashboard, definition => definition.Key == PermissionKeys.DashboardViewOwn);
        Assert.Contains(dashboard, definition => definition.Key == PermissionKeys.DashboardViewTeam);
        Assert.Contains(dashboard, definition => definition.Key == PermissionKeys.DashboardViewOrganization);
        Assert.Contains(dashboard, definition => definition.Key == PermissionKeys.DashboardAnalytics);
    }

    [Fact]
    public void ReferencesAndScopeGroupsPointAtRealEntries()
    {
        var keys = PermissionCatalog.All.Select(definition => definition.Key).ToHashSet();

        foreach (var definition in PermissionCatalog.All)
        {
            if (definition.Requires is not null)
            {
                Assert.Contains(definition.Requires, keys);
            }

            if (definition.GrantFrom is not null)
            {
                Assert.True(
                    keys.Contains(definition.GrantFrom) || EffectivePermissions.IsLegacy(definition.GrantFrom),
                    $"{definition.Key} is granted from unknown {definition.GrantFrom}");
            }
        }

        foreach (var group in PermissionCatalog.All.Where(definition => definition.ScopeGroup is not null).GroupBy(definition => definition.ScopeGroup))
        {
            var ranks = group.Select(definition => definition.ScopeRank).Order().ToList();
            Assert.Equal(Enumerable.Range(1, ranks.Count), ranks);
        }
    }

    [Fact]
    public void AlwaysOnEntriesAreNeverAssignableOrStored()
    {
        Assert.All(PermissionCatalog.All.Where(definition => definition.AlwaysGranted), definition => Assert.False(definition.IsAssignable, definition.Key));
        Assert.DoesNotContain(PermissionCatalog.Stored, definition => definition.AlwaysGranted);
    }

    [Fact]
    public void EveryPermissionKeyConstantIsInTheCatalogueOrIsALegacyKey()
    {
        var constants = typeof(PermissionKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!);
        var keys = PermissionCatalog.All.Select(definition => definition.Key).ToHashSet();

        foreach (var (name, key) in constants)
        {
            if (name.StartsWith("Legacy", StringComparison.Ordinal))
            {
                Assert.True(EffectivePermissions.IsLegacy(key), name);
                Assert.DoesNotContain(key, keys);
            }
            else
            {
                Assert.Contains(key, keys);
            }
        }

        Assert.Equal(keys.Count, constants.Count(pair => !pair.Key.StartsWith("Legacy", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheFrontendMirrorsTheStoredCatalogueKeys()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(ThisFile())!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "frontend", "src", "app", "core", "auth", "permissions.ts")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory!.FullName, "frontend", "src", "app", "core", "auth", "permissions.ts"));
        var block = source[source.IndexOf("export const Permissions = {", StringComparison.Ordinal)..source.IndexOf("} as const;", StringComparison.Ordinal)];
        var frontendKeys = System.Text.RegularExpressions.Regex.Matches(block, @":\s*'([a-z.]+)'")
            .Select(match => match.Groups[1].Value)
            .Order()
            .ToList();

        Assert.Equal(PermissionCatalog.Stored.Select(definition => definition.Key).Order(), frontendKeys);
    }

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    // ---------------------------------------------------------------- effective permissions

    [Fact]
    public void AnOldBroadKeyGrantsExactlyTheFinerKeysItReplaced()
    {
        var effective = EffectivePermissions.Expand([PermissionKeys.LegacyEmployeesManage]);

        Assert.Contains(PermissionKeys.EmployeesCreate, effective);
        Assert.Contains(PermissionKeys.EmployeesEdit, effective);
        Assert.Contains(PermissionKeys.EmployeesStatus, effective);
        Assert.Contains(PermissionKeys.EmployeesMfaReset, effective);
        // Deleting a user and changing a role needed Role Configuration before, never the employee management key.
        Assert.DoesNotContain(PermissionKeys.EmployeesDelete, effective);
        Assert.DoesNotContain(PermissionKeys.EmployeesRoleAssign, effective);
    }

    [Fact]
    public void ALongerScopeIncludesTheLowerOnesButNeverAlwaysOnEntries()
    {
        var organization = EffectivePermissions.Expand([PermissionKeys.DashboardViewOrganization]);
        var allTasks = EffectivePermissions.Expand([PermissionKeys.TasksViewAll]);

        Assert.Contains(PermissionKeys.DashboardViewTeam, organization);
        Assert.DoesNotContain(PermissionKeys.DashboardViewOwn, organization);
        Assert.Equal([PermissionKeys.TasksViewAll], allTasks);
    }

    [Fact]
    public void VisibleDropsOldKeysAndKeepsCatalogueOrder()
    {
        var visible = EffectivePermissions.Visible(
            EffectivePermissions.Expand([PermissionKeys.LegacyProjectsManage, PermissionKeys.ProjectsView]));

        Assert.DoesNotContain(PermissionKeys.LegacyProjectsManage, visible);
        Assert.Contains(PermissionKeys.ProjectsDelete, visible);
        Assert.Equal(visible.Order(StringComparer.Ordinal), visible);
    }

    // ---------------------------------------------------------------- seeding and upgrade

    [Fact]
    public async System.Threading.Tasks.Task IntroducingTheFinerKeysKeepsEveryExistingRolesAccess()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        // The database as it was before the finer permissions: the old keys exist, roles hold them.
        var oldKeys = new[]
        {
            PermissionKeys.LegacyEmployeesManage, PermissionKeys.EmployeesView, PermissionKeys.ReportsView,
            PermissionKeys.LegacyProjectsManage, PermissionKeys.ProjectsView, PermissionKeys.TasksCreate
        };
        var rows = new Dictionary<string, Permission>();
        foreach (var key in oldKeys)
        {
            var existing = await db.Permissions.FirstOrDefaultAsync(permission => permission.Key == key);
            if (existing is null)
            {
                existing = new Permission { Key = key, Name = key, Description = key, Group = "Old" };
                db.Permissions.Add(existing);
            }

            rows[key] = existing;
        }

        await db.SaveChangesAsync();

        var custom = new Role { Name = "People and reports", CreatedAt = DateTime.UtcNow };
        foreach (var key in new[] { PermissionKeys.LegacyEmployeesManage, PermissionKeys.EmployeesView, PermissionKeys.ReportsView })
        {
            custom.Permissions.Add(new RolePermission { Permission = rows[key] });
        }

        var viewer = new Role { Name = "Viewer only", CreatedAt = DateTime.UtcNow };
        viewer.Permissions.Add(new RolePermission { Permission = rows[PermissionKeys.ProjectsView] });
        db.Roles.AddRange(custom, viewer);
        await db.SaveChangesAsync();

        // Access before: derived from the old keys alone.
        var customBefore = EffectivePermissions.Expand(oldKeys.Where(key => key != PermissionKeys.LegacyProjectsManage && key != PermissionKeys.ProjectsView && key != PermissionKeys.TasksCreate));

        await new AccessControlSeeder(db, TimeProvider.System).SeedAsync();

        async System.Threading.Tasks.Task<IReadOnlySet<string>> EffectiveAsync(int roleId) => EffectivePermissions.Expand(
            await db.RolePermissions.Where(grant => grant.RoleId == roleId).Select(grant => grant.Permission.Key).ToListAsync());

        var customAfter = await EffectiveAsync(custom.Id);
        var viewerAfter = await EffectiveAsync(viewer.Id);

        // The role keeps everything it had and the export that used to ride on report access...
        Assert.True(customBefore.IsSubsetOf(customAfter));
        Assert.Contains(PermissionKeys.ReportsExport, customAfter);
        Assert.Contains(PermissionKeys.EmployeesCreate, customAfter);
        // ...and gains nothing it could not do: no delete, no role assignment, no organization data.
        Assert.DoesNotContain(PermissionKeys.EmployeesDelete, customAfter);
        Assert.DoesNotContain(PermissionKeys.EmployeesRoleAssign, customAfter);
        Assert.DoesNotContain(PermissionKeys.TasksDelete, customAfter);
        Assert.DoesNotContain(PermissionKeys.DashboardViewOrganization, customAfter);

        // A role that only viewed projects gets only what used to be open to everyone.
        Assert.Contains(PermissionKeys.TasksStatus, viewerAfter);
        Assert.Contains(PermissionKeys.DashboardAnalytics, viewerAfter);
        Assert.DoesNotContain(PermissionKeys.ReportsExport, viewerAfter);
        Assert.DoesNotContain(PermissionKeys.ProjectsCreate, viewerAfter);

        Assert.Equal(2, await db.Roles.CountAsync(role => !role.IsSystem));
    }

    [Fact]
    public async System.Threading.Tasks.Task ManagerAndEmployeeKeepExactlyTheirPreviousEffectiveAccess()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        await new AccessControlSeeder(db, TimeProvider.System).SeedAsync();

        async System.Threading.Tasks.Task<IReadOnlySet<string>> EffectiveAsync(int roleId) => EffectivePermissions.Expand(
            await db.RolePermissions.Where(grant => grant.RoleId == roleId).Select(grant => grant.Permission.Key).ToListAsync());

        var manager = await EffectiveAsync(SystemRoles.ManagerId);
        var employee = await EffectiveAsync(SystemRoles.EmployeeId);

        string[] managerBefore =
        [
            PermissionKeys.DashboardViewTeam, PermissionKeys.ProjectsView, PermissionKeys.DepartmentsView,
            PermissionKeys.EmployeesView, PermissionKeys.TasksViewAll, PermissionKeys.TasksCreate, PermissionKeys.TasksUpdate,
            PermissionKeys.TasksReassign, PermissionKeys.TasksRecipients, PermissionKeys.ReportsView, PermissionKeys.ReportsExport,
            PermissionKeys.WorkloadView, PermissionKeys.SettingsReminders
        ];

        Assert.True(managerBefore.ToHashSet().IsSubsetOf(manager));
        Assert.DoesNotContain(PermissionKeys.TasksDelete, manager);
        Assert.DoesNotContain(PermissionKeys.ProjectsCreate, manager);
        Assert.DoesNotContain(PermissionKeys.EmployeesCreate, manager);
        Assert.DoesNotContain(PermissionKeys.DashboardViewOrganization, manager);
        Assert.DoesNotContain(PermissionKeys.SettingsApplication, manager);
        Assert.DoesNotContain(PermissionKeys.RolesManage, manager);

        Assert.DoesNotContain(PermissionKeys.TasksViewAll, employee);
        Assert.DoesNotContain(PermissionKeys.TasksCreate, employee);
        Assert.DoesNotContain(PermissionKeys.DashboardViewTeam, employee);
        Assert.DoesNotContain(PermissionKeys.ReportsView, employee);
    }

    [Fact]
    public async System.Threading.Tasks.Task SavingARoleOnceDoesNotBringBackAFinerKeyThatWasRemoved()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        await new AccessControlSeeder(db, TimeProvider.System).SeedAsync();

        var role = new Role { Name = "No exports", CreatedAt = DateTime.UtcNow };
        role.Permissions.Add(new RolePermission { Permission = await db.Permissions.SingleAsync(permission => permission.Key == PermissionKeys.ReportsView) });
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        await new AccessControlSeeder(db, TimeProvider.System).SeedAsync();
        await new AccessControlSeeder(db, TimeProvider.System).SeedAsync();

        var keys = await db.RolePermissions.Where(grant => grant.RoleId == role.Id).Select(grant => grant.Permission.Key).ToListAsync();
        Assert.Equal([PermissionKeys.ReportsView], keys);
    }
}
