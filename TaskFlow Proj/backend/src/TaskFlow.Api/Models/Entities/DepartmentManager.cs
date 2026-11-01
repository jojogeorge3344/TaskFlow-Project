namespace TaskFlow.Api.Models.Entities;

/// <summary>A department a manager manages. Membership is managed from the Department Management screen.</summary>
public class DepartmentManager
{
    public int ManagerUserId { get; set; }

    public User Manager { get; set; } = null!;

    public int DepartmentId { get; set; }

    public Department Department { get; set; } = null!;
}
