namespace TaskFlow.Api.Models.Enums;

/// <summary>Processing state of a reminder history row. The reminder engine writes only Sent;
/// the other states are reserved for email delivery (Screen 14).</summary>
public enum ReminderStatus
{
    Pending,
    Sent,
    Failed,
    Skipped
}
