namespace TaskFlow.Api.Models.Entities;

public class Department
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<DepartmentManager> ManagerLinks { get; set; } = new List<DepartmentManager>();

    public ICollection<UserDepartment> Memberships { get; set; } = new List<UserDepartment>();
}
