namespace TaskFlow.Domain.Enums;

/// <summary>Processing state of a reminder history row. The reminder engine writes only Sent (the in-app
/// reminder was generated); email delivery has its own status on <c>EmailDeliveryLog</c>.</summary>
public enum ReminderStatus
{
    Pending,
    Sent,
    Failed,
    Skipped
}
