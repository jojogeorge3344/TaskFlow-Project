using TaskFlow.Api.Dtos.Settings.RoleConfiguration;

namespace TaskFlow.Api.Services.Interfaces.Settings.RoleConfiguration;

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken);

    Task<RoleDto> GetRoleAsync(int id, CancellationToken cancellationToken);

    /// <summary>The fixed permission catalog, in display order.</summary>
    Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken);

    Task<RoleDto> CreateAsync(SaveRoleRequest request, CancellationToken cancellationToken);

    /// <summary>Renames a custom role and replaces its permissions. System roles are refused.</summary>
    Task<RoleDto> UpdateAsync(int id, SaveRoleRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes a custom role that no user holds. System roles are refused.</summary>
    System.Threading.Tasks.Task DeleteAsync(int id, CancellationToken cancellationToken);
}