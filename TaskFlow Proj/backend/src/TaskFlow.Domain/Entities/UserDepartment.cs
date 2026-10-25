namespace TaskFlow.Domain.Entities;

/// <summary>A department a user belongs to. A user may belong to several.</summary>
public class UserDepartment
{
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public int DepartmentId { get; set; }

    public Department Department { get; set; } = null!;
}
