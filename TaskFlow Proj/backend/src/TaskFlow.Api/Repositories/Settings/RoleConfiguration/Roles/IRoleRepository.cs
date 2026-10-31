using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Settings.RoleConfiguration.Roles;

public interface IRoleRepository : IBaseRepository<Role>
{
    /// <summary>Every role, system roles first, then in the order they were created. Holders include deleted accounts.</summary>
    Task<IReadOnlyList<RoleSummary>> GetSummariesAsync(CancellationToken cancellationToken = default);

    /// <summary>The tracked role with its permission grants, for changing them.</summary>
    Task<Role?> GetWithPermissionsForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Whether another role already has this name. The name column compares case-insensitively.</summary>
    Task<bool> NameTakenAsync(string name, int? exceptRoleId, CancellationToken cancellationToken = default);

    /// <summary>The permission keys granted to the role with this name, as stored.</summary>
    Task<IReadOnlyList<string>> GetPermissionKeysByRoleNameAsync(string roleName, CancellationToken cancellationToken = default);
}
