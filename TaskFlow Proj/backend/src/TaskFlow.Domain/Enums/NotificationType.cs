namespace TaskFlow.Domain.Enums;

public enum NotificationType
{
    TaskAssigned,
    Reassigned,
    DeadlineReminder,
    Overdue,
    System,

    /// <summary>An administrator processed a password recovery request for this account.</summary>
    PasswordReset
}