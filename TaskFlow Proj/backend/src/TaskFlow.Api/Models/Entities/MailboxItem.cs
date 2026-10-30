using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Models.Entities;

/// <summary>One user's copy of a mail. Read state, flags and trash are personal; privacy is "the caller owns
/// the item". Deleting forever sets <see cref="PurgedAt"/> and keeps the row.</summary>
public class MailboxItem
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public MailMessage? Message { get; set; }
    public int OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }
    public MailFolder Folder { get; set; }
    public bool IsRead { get; set; }
    public bool IsStarred { get; set; }
    public bool IsImportant { get; set; }
    public DateTime? TrashedAt { get; set; }
    public DateTime? PurgedAt { get; set; }

    /// <summary>List order: when the mail was sent, or when a draft was last saved.</summary>
    public DateTime SortAt { get; set; }
}
