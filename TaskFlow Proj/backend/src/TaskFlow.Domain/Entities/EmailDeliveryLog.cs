using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

/// <summary>One email: the outbox row the dispatcher sends from and the delivery log Screen 14 shows.
/// A reminder email links to the reminder history row that produced it (at most one email per row).</summary>
public class EmailDeliveryLog
{
    public int Id { get; set; }
    public EmailKind Kind { get; set; }
    public int? ReminderHistoryId { get; set; }
    public ReminderHistory? ReminderHistory { get; set; }
    public int? TaskId { get; set; }
    public Task? Task { get; set; }
    public int? RecipientUserId { get; set; }
    public User? RecipientUser { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public EmailDeliveryStatus Status { get; set; }
    public int Attempts { get; set; }

    /// <summary>UTC time the dispatcher may next try this email (send time, retry backoff or claim lease).</summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>UTC end of the reminder's local day; a reminder email still pending then is skipped.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>The task's deadline when the email was queued; a different deadline at send time means the
    /// task was rescheduled and the email is skipped.</summary>
    public DateTime? TaskDeadline { get; set; }

    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }

    /// <summary>The Administrator who sent a test email.</summary>
    public int? CreatedByUserId { get; set; }
}
