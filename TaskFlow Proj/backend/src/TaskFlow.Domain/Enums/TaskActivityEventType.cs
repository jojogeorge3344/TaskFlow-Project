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
    Administrative
}