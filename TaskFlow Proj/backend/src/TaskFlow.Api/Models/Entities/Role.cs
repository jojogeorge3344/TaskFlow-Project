namespace TaskFlow.Api.Models.Entities;

/// <summary>A named set of permissions. The three system roles are created by the application and are read-only;
/// every other role is a custom role an administrator manages.</summary>
public class Role
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();

    public ICollection<User> Users { get; set; } = new List<User>();
}
