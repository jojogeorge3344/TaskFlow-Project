namespace TaskFlow.Domain.Entities;

/// <summary>A user's share of their time on one project, and the position they hold there. A user's percentages across all
/// projects total at most 100.</summary>
public class UserProjectAllocation
{
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public int ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    /// <summary>Whole percentage, 1 to 100.</summary>
    public int AllocationPercent { get; set; }

    /// <summary>True when this person manages the project. A project's team that has members has at least one such row, and
    /// the manager-project link of each flagged person is kept in step with it.</summary>
    public bool IsProjectManager { get; set; }

    public int PositionId { get; set; }

    public Position Position { get; set; } = null!;
}
