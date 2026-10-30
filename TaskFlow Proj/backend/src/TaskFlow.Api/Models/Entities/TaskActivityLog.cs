using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

/// <summary>An immutable, chronological entry in a task's activity history.</summary>
public class TaskActivityLog
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public Domain.Entities.Task? Task { get; set; }
    public TaskActivityEventType EventType { get; set; }
    public int ActorUserId { get; set; }
    public User? Actor { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}