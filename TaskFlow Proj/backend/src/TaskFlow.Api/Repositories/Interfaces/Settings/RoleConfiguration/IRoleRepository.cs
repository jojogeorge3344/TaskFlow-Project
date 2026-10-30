using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Interfaces.Settings.RoleConfiguration;

/// <summary>A role with how many accounts hold it and the permission keys it is granted, as stored.</summary>
public record RoleSummary(int Id, string Name, bool IsSystem, int UserCount, IReadOnlyList<string> PermissionKeys);

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
