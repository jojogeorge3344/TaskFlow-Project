using TaskFlow.Application.Mail;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Infrastructure.Mail;

/// <summary>Writes a system mail the way <see cref="Services.ReminderService"/> does for deadline
/// reminders: one <see cref="MailMessage"/> with <see cref="MailMessage.IsSystem"/> set (so the sender
/// is the "TaskFlow" system sender, not a user), a <see cref="MailRecipient"/> row per recipient and a
/// <see cref="MailboxItem"/> in each recipient's Inbox.</summary>
public sealed class SystemMailWriter : ISystemMailWriter
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public SystemMailWriter(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task WriteAsync(
        IReadOnlyCollection<int> recipientUserIds,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (recipientUserIds.Count == 0)
        {
            return;
        }

        if (subject.Length > MailRules.MaxSubjectLength)
        {
            subject = subject[..MailRules.MaxSubjectLength];
        }

        if (body.Length > MailRules.MaxBodyLength)
        {
            body = body[..MailRules.MaxBodyLength];
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var message = new MailMessage
        {
            ConversationId = Guid.NewGuid(),
            IsSystem = true,
            Subject = subject,
            Body = body,
            CreatedAt = now,
            UpdatedAt = now,
            SentAt = now
        };

        _dbContext.MailMessages.Add(message);

        foreach (var userId in recipientUserIds.Distinct())
        {
            _dbContext.MailRecipients.Add(new MailRecipient
            {
                Message = message,
                UserId = userId,
                Kind = MailRecipientKind.To
            });
            _dbContext.MailboxItems.Add(new MailboxItem
            {
                Message = message,
                OwnerUserId = userId,
                Folder = MailFolder.Inbox,
                SortAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
