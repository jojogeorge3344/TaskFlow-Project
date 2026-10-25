using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.UnitTests;

/// <summary>Test databases are built with EnsureCreated, which skips the migration, so the system roles, the permission
/// catalog and their grants are added here exactly as the application adds them at startup.</summary>
public static class TestAccessControl
{
    public static void Seed(AppDbContext db) =>
        new AccessControlSeeder(db, TimeProvider.System).SeedAsync().GetAwaiter().GetResult();

    public static System.Threading.Tasks.Task SeedAsync(AppDbContext db) =>
        new AccessControlSeeder(db, TimeProvider.System).SeedAsync();

    /// <summary>A custom role holding exactly the named permissions.</summary>
    public static Role AddCustomRole(AppDbContext db, string name, params string[] permissionKeys)
    {
        var role = new Role { Name = name, IsSystem = false, CreatedAt = DateTime.UtcNow };
        foreach (var permission in db.Permissions.Where(permission => permissionKeys.Contains(permission.Key)).ToList())
        {
            role.Permissions.Add(new RolePermission { Permission = permission });
        }

        db.Roles.Add(role);
        db.SaveChanges();
        return role;
    }

    public static string NameFor(int roleId) => roleId switch
    {
        SystemRoles.AdministratorId => SystemRoles.Administrator,
        SystemRoles.ManagerId => SystemRoles.Manager,
        SystemRoles.EmployeeId => SystemRoles.Employee,
        _ => throw new ArgumentOutOfRangeException(nameof(roleId), roleId, "Not a system role.")
    };

    public static int IdFor(string roleName) => roleName switch
    {
        SystemRoles.Administrator => SystemRoles.AdministratorId,
        SystemRoles.Manager => SystemRoles.ManagerId,
        SystemRoles.Employee => SystemRoles.EmployeeId,
        _ => throw new ArgumentOutOfRangeException(nameof(roleName), roleName, "Not a system role.")
    };
}
