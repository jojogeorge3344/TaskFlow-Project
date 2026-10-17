using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

/// <summary>One generated deadline reminder for one recipient. The unique index on
/// (TaskId, RecipientUserId, ReminderType, ScheduledFor) prevents duplicate reminders (BRD §3.5).</summary>
public class ReminderHistory
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public Task? Task { get; set; }
    public int RecipientUserId { get; set; }
    public User? RecipientUser { get; set; }
    public ReminderType ReminderType { get; set; }

    /// <summary>UTC instant of local midnight (application timezone) of the reminder's day.</summary>
    public DateTime ScheduledFor { get; set; }

    public ReminderStatus Status { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int DeliveryAttempts { get; set; }
}
