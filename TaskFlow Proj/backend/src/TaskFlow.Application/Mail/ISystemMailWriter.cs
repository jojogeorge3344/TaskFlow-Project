namespace TaskFlow.Application.Mail;

/// <summary>Write seam for mail the system itself sends (the "TaskFlow" sender, <c>IsSystem = true</c>),
/// so a feature flow can deliver an internal mail without reaching into the mail tables. One message,
/// one <c>MailRecipient</c> per recipient, one Inbox <c>MailboxItem</c> per recipient - the shape
/// <c>ReminderService</c> established for deadline reminders. Failures surface as exceptions; the
/// caller decides what that means for the operation it is part of.</summary>
public interface ISystemMailWriter
{
    /// <summary>Writes a system mail to the given recipients' Inboxes. An empty recipient list writes
    /// nothing and succeeds.</summary>
    Task WriteAsync(
        IReadOnlyCollection<int> recipientUserIds,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}
