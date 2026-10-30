namespace TaskFlow.Domain.Enums;

/// <summary>Which copy of a mail a mailbox item is: received, sent or an unsent draft. Starred, Important and
/// Trash are views over these items, not folders of their own.</summary>
public enum MailFolder
{
    Inbox,
    Sent,
    Drafts
}
