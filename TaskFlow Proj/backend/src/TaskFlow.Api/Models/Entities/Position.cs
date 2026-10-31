namespace TaskFlow.Api.Models.Entities;

/// <summary>A role someone holds on a project, such as QA or Developer. A label only: it grants no permissions.</summary>
public class Position
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<UserProjectAllocation> Allocations { get; set; } = new List<UserProjectAllocation>();
}
