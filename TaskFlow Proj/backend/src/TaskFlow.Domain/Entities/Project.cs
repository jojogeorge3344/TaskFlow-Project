namespace TaskFlow.Domain.Entities;

public class Project
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<UserProjectAllocation> Allocations { get; set; } = new List<UserProjectAllocation>();
}
