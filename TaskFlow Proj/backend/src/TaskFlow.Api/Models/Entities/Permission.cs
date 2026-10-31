namespace TaskFlow.Api.Models.Entities;

/// <summary>One entry of the fixed permission catalog. Rows are created from <see cref="Security.PermissionCatalog"/>
/// and are never updated or deleted.</summary>
public class Permission
{
    public int Id { get; set; }

    /// <summary>The stable identifier code checks, for example <c>tasks.create</c>.</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>The area the permission is shown under on the Permissions Management screen.</summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>False for permissions only the Administrator system role may hold.</summary>
    public bool IsAssignable { get; set; } = true;

    public ICollection<RolePermission> Roles { get; set; } = new List<RolePermission>();
}
