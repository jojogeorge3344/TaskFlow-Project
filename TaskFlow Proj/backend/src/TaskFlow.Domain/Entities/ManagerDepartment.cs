namespace TaskFlow.Domain.Entities;

/// <summary>A department a manager is assigned to. Recorded data only; it does not narrow what the manager can see.</summary>
public class ManagerDepartment
{
    public int ManagerUserId { get; set; }

    public User Manager { get; set; } = null!;

    public int DepartmentId { get; set; }

    public Department Department { get; set; } = null!;
}
