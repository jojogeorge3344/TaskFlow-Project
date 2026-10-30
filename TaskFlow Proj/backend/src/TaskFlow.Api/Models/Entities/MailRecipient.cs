using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

/// <summary>A TaskFlow user a sent mail was addressed to.</summary>
public class MailRecipient
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public MailMessage? Message { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public MailRecipientKind Kind { get; set; }
}
