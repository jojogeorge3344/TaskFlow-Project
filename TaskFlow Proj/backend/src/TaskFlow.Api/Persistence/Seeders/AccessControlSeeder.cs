using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.Api.Persistence.Seeders;

/// <summary>
/// Makes sure the three system roles, the permission catalog and the system roles' grants exist. This is required
/// configuration, not demo data, so it runs on every start whether or not account seeding is enabled.
/// <list type="bullet">
/// <item>A permission missing from the database is added; the Manager and Employee receive it only at that moment, from
/// <see cref="PermissionDefinition"/>, so later changes by anyone never get overwritten.</item>
/// <item>The Administrator is granted every permission on every start, including ones added in later releases.</item>
/// </list>
/// Only inserts happen here: the database refuses updates and deletes of system rows.
/// </summary>
public class AccessControlSeeder
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AccessControlSeeder(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async System.Threading.Tasks.Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSystemRolesAsync(cancellationToken);

        var existing = await _db.Permissions.ToDictionaryAsync(permission => permission.Key, cancellationToken);
        var created = new List<(Permission Permission, PermissionDefinition Definition)>();

        foreach (var definition in PermissionCatalog.Stored)
        {
            if (existing.ContainsKey(definition.Key))
            {
                continue;
            }

            var permission = new Permission
            {
                Key = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                Group = definition.Group,
                IsAssignable = definition.IsAssignable
            };

            _db.Permissions.Add(permission);
            created.Add((permission, definition));
            existing[definition.Key] = permission;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var adminGrants = await _db.RolePermissions
            .Where(grant => grant.RoleId == SystemRoles.AdministratorId)
            .Select(grant => grant.PermissionId)
            .ToListAsync(cancellationToken);

        foreach (var permission in existing.Values.Where(permission => !adminGrants.Contains(permission.Id)))
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = SystemRoles.AdministratorId, PermissionId = permission.Id });
        }

        if (created.Count > 0)
        {
            var roleIds = await _db.Roles.Select(role => role.Id).ToListAsync(cancellationToken);
            var grantsByKey = (await _db.RolePermissions
                    .Select(grant => new { grant.RoleId, grant.Permission.Key })
                    .ToListAsync(cancellationToken))
                .GroupBy(grant => grant.Key)
                .ToDictionary(group => group.Key, group => group.Select(grant => grant.RoleId).ToList());

            foreach (var (permission, definition) in created)
            {
                var recipients = new HashSet<int>();

                if (definition.GrantedToManager)
                {
                    recipients.Add(SystemRoles.ManagerId);
                }

                if (definition.GrantedToEmployee)
                {
                    recipients.Add(SystemRoles.EmployeeId);
                }

                // The feature used to be open to every signed-in user, so nobody who has it today may lose it.
                if (definition.GrantToAllExistingRoles)
                {
                    recipients.UnionWith(roleIds);
                }

                // The feature used to be covered by another permission; whoever held that keeps this one.
                if (definition.GrantFrom is not null && grantsByKey.TryGetValue(definition.GrantFrom, out var holders))
                {
                    recipients.UnionWith(holders);
                }

                recipients.Remove(SystemRoles.AdministratorId);
                foreach (var roleId in recipients)
                {
                    _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id });
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The migration creates the system roles; this also covers a database built without it.</summary>
    private async System.Threading.Tasks.Task EnsureSystemRolesAsync(CancellationToken cancellationToken)
    {
        var existingIds = await _db.Roles.Select(role => role.Id).ToListAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var (id, name) in new[]
        {
            (SystemRoles.AdministratorId, SystemRoles.Administrator),
            (SystemRoles.ManagerId, SystemRoles.Manager),
            (SystemRoles.EmployeeId, SystemRoles.Employee)
        })
        {
            if (!existingIds.Contains(id))
            {
                _db.Roles.Add(new Role { Id = id, Name = name, IsSystem = true, CreatedAt = now });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
