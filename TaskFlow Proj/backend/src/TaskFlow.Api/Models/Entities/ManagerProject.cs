namespace TaskFlow.Api.Models.Entities;

/// <summary>A project a manager oversees. Recorded data only; it does not narrow what the manager can see.</summary>
public class ManagerProject
{
    public int ManagerUserId { get; set; }

    public User Manager { get; set; } = null!;

    public int ProjectId { get; set; }

    public Project Project { get; set; } = null!;
}
