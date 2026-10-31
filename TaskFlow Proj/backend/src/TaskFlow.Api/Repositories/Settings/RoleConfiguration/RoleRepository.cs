using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Repositories.Settings.RoleConfiguration;

public class RoleRepository : BaseRepository<Role>, IRoleRepository
{
    public RoleRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<RoleSummary>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await Set.AsNoTracking()
            .OrderByDescending(role => role.IsSystem)
            .ThenBy(role => role.Id)
            .Select(role => new
            {
                role.Id,
                role.Name,
                role.IsSystem,
                UserCount = Db.Users.IgnoreQueryFilters().Count(user => user.RoleId == role.Id),
                Keys = role.Permissions.Select(grant => grant.Permission.Key).ToList()
            })
            .ToListAsync(cancellationToken);

        return roles.Select(role => new RoleSummary(role.Id, role.Name, role.IsSystem, role.UserCount, role.Keys)).ToList();
    }

    public Task<Role?> GetWithPermissionsForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        Set.Include(role => role.Permissions).FirstOrDefaultAsync(role => role.Id == id, cancellationToken);

    public Task<bool> NameTakenAsync(string name, int? exceptRoleId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(role => role.Name == name && role.Id != exceptRoleId, cancellationToken);

    public async Task<IReadOnlyList<string>> GetPermissionKeysByRoleNameAsync(string roleName, CancellationToken cancellationToken = default) =>
        await Db.RolePermissions.AsNoTracking()
            .Where(grant => grant.Role.Name == roleName)
            .Select(grant => grant.Permission.Key)
            .ToListAsync(cancellationToken);
}
