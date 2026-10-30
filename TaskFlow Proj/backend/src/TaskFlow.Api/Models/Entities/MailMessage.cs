namespace TaskFlow.Domain.Entities;

/// <summary>The content of one internal mail. Written once when sent; while <see cref="SentAt"/> is null it is
/// the author's draft. Each person's copy (folder, read state, flags, trash) is a <see cref="MailboxItem"/>.</summary>
public class MailMessage
{
    public int Id { get; set; }

    /// <summary>Shared by a mail and every reply or forward made from it.</summary>
    public Guid ConversationId { get; set; }

    public int? ParentMessageId { get; set; }
    public MailMessage? ParentMessage { get; set; }

    /// <summary>Null for mails from the TaskFlow system sender (deadline reminders).</summary>
    public int? SenderUserId { get; set; }
    public User? SenderUser { get; set; }

    public bool IsSystem { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public int? TaskId { get; set; }
    public Task? Task { get; set; }

    /// <summary>The reminder this mail was generated for; unique, so a reminder event yields at most one mail.</summary>
    public int? ReminderHistoryId { get; set; }
    public ReminderHistory? ReminderHistory { get; set; }

    /// <summary>Raw addresses typed into a draft; resolved to recipients only when the draft is sent.</summary>
    public string? DraftTo { get; set; }
    public string? DraftCc { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? SentAt { get; set; }

    public ICollection<MailRecipient> Recipients { get; set; } = new List<MailRecipient>();
    public ICollection<MailAttachment> Attachments { get; set; } = new List<MailAttachment>();
}
