namespace TaskFlow.Api.Models.Enums;

/// <summary>The kind of deadline reminder a history row records. Part of the reminder dedup key
/// (TaskId, RecipientUserId, ReminderType, ScheduledFor).</summary>
public enum ReminderType
{
    FirstReminder,
    SecondReminder,
    DueToday,
    Overdue
}
