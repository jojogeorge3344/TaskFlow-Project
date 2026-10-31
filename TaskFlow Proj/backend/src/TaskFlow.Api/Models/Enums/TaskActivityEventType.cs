namespace TaskFlow.Api.Models.Enums;

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
    HoursLogged,
    TimeLogged,
    /// <summary>Appended last: the value is stored as text, so existing rows keep their meaning.</summary>
    Deleted,
    CommentEdited,
    CommentDeleted
}