namespace TaskFlow.Domain.Enums;

public enum TaskActivityEventType
{
    Created,
    Assigned,
    Reassigned,
    StatusChanged,
    DeadlineChanged,
    PriorityChanged,
    Completed,
    CommentAdded,
    Administrative,
    /// <summary>Appended last: the value is stored as text, so existing rows keep their meaning.</summary>
    Deleted
}