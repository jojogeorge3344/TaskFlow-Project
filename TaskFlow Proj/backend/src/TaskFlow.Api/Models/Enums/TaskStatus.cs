namespace TaskFlow.Domain.Enums;

public enum TaskStatus
{
    ToDo,
    InProgress,
    Blocked,
    Completed,
    Cancelled,
    /// <summary>Appended last so existing stored integer values keep their meaning.</summary>
    InQA
}
