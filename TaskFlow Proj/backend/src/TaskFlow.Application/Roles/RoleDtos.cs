namespace TaskFlow.Application.Roles;

public record PermissionDto(int Id, string Key, string Name, string Description, string Group, bool IsAssignable);

/// <summary>A role with the keys of the permissions it holds. <see cref="UserCount"/> counts every account holding it.</summary>
public record RoleDto(int Id, string Name, bool IsSystem, int UserCount, IReadOnlyList<string> PermissionKeys);

public record SaveRoleRequest(string? Name, IReadOnlyList<string>? PermissionKeys);

public enum RoleFailure
{
    NotFound,

    /// <summary>The name or the permission list is not acceptable.</summary>
    Invalid,

    /// <summary>The role or permission is part of the fixed, read-only set.</summary>
    ReadOnly,

    /// <summary>Another role already has that name.</summary>
    Duplicate,

    /// <summary>Users still hold the role.</summary>
    InUse
}

/// <summary>A role or permission rule was broken. The message is safe to show to the administrator.</summary>
public class RoleException : Exception
{
    public RoleException(RoleFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    public RoleFailure Failure { get; }
}

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
