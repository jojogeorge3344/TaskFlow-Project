using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common.Models;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Communication.Mail.Messages;

public class MailRepository : BaseRepository<MailMessage>, IMailRepository
{
    public MailRepository(AppDbContext db)
        : base(db)
    {
    }

    /// <summary>The owner's items that have not been deleted forever.</summary>
    private IQueryable<MailboxItem> Mine(int ownerId) =>
        Db.MailboxItems.Where(item => item.OwnerUserId == ownerId && item.PurgedAt == null);

    public Task<MailboxItem?> GetDraftForUpdateAsync(int ownerId, int draftMessageId, CancellationToken cancellationToken = default) =>
        Mine(ownerId)
            .Include(item => item.Message)
            .FirstOrDefaultAsync(item => item.MessageId == draftMessageId && item.Folder == MailFolder.Drafts, cancellationToken);

    public Task<MailMessage?> GetMessageOfItemAsync(int ownerId, int itemId, CancellationToken cancellationToken = default) =>
        Mine(ownerId)
            .Where(item => item.Id == itemId && item.Folder != MailFolder.Drafts)
            .Select(item => item.Message)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int?> GetMessageIdOfItemAsync(int ownerId, int itemId, CancellationToken cancellationToken = default) =>
        Mine(ownerId)
            .Where(item => item.Id == itemId && item.Folder != MailFolder.Drafts)
            .Select(item => (int?)item.MessageId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<MailboxItem?> GetItemForUpdateAsync(int ownerId, int itemId, CancellationToken cancellationToken = default) =>
        Mine(ownerId).FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);

    public Task<MailItemHeader?> GetItemHeaderAsync(int ownerId, int itemId, CancellationToken cancellationToken = default) =>
        Mine(ownerId)
            .AsNoTracking()
            .Where(item => item.Id == itemId)
            .Select(item => new MailItemHeader(
                item.Id,
                item.MessageId,
                item.Folder,
                item.IsRead,
                item.IsStarred,
                item.IsImportant,
                item.TrashedAt != null,
                item.Message!.ConversationId,
                item.Message.DraftTo,
                item.Message.DraftCc))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> GetConversationMessageIdsAsync(int ownerId, Guid conversationId, CancellationToken cancellationToken = default) =>
        await Mine(ownerId)
            .Where(item => item.Folder != MailFolder.Drafts && item.Message!.ConversationId == conversationId)
            .Select(item => item.MessageId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(int ownerId, MailView view, bool unreadOnly, CancellationToken cancellationToken = default)
    {
        var items = InView(Mine(ownerId), view);
        return unreadOnly ? items.CountAsync(item => !item.IsRead, cancellationToken) : items.CountAsync(cancellationToken);
    }

    public async Task<PagedResult<MailListItemDto>> ListAsync(
        int ownerId,
        MailView view,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var items = InView(Mine(ownerId), view);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = "%" + EscapeLike(search.Trim()) + "%";
            items = items.Where(item =>
                EF.Functions.Like(item.Message!.Subject, pattern, "\\")
                || EF.Functions.Like(item.Message!.Body, pattern, "\\")
                || (item.Message!.DraftTo != null && EF.Functions.Like(item.Message!.DraftTo, pattern, "\\"))
                || (item.Message!.IsSystem && EF.Functions.Like(MailRules.SystemSenderName, pattern, "\\"))
                || Db.Users.IgnoreQueryFilters().Any(user => user.Id == item.Message!.SenderUserId
                    && (EF.Functions.Like(user.FullName, pattern, "\\") || EF.Functions.Like(user.Email, pattern, "\\")))
                || Db.MailRecipients.Any(recipient => recipient.MessageId == item.MessageId
                    && Db.Users.IgnoreQueryFilters().Any(user => user.Id == recipient.UserId
                        && (EF.Functions.Like(user.FullName, pattern, "\\") || EF.Functions.Like(user.Email, pattern, "\\")))));
        }

        var total = await items.CountAsync(cancellationToken);
        var rows = await items
            .OrderByDescending(item => item.SortAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                item.Id,
                item.MessageId,
                item.Folder,
                item.IsRead,
                item.IsStarred,
                item.IsImportant,
                item.TrashedAt,
                item.SortAt,
                item.Message!.SenderUserId,
                item.Message.IsSystem,
                item.Message.Subject,
                BodyStart = item.Message.Body.Substring(0, 300),
                item.Message.DraftTo,
                HasAttachments = item.Message.Attachments.Any(),
                RecipientIds = item.Message.Recipients.Where(recipient => recipient.Kind == MailRecipientKind.To).Select(recipient => recipient.UserId).ToList()
            })
            .ToListAsync(cancellationToken);

        var people = await LoadPeopleAsync(rows.SelectMany(row => row.RecipientIds.Append(row.SenderUserId ?? 0)), cancellationToken);
        var result = rows
            .Select(row => new MailListItemDto(
                row.Id,
                row.MessageId,
                row.Folder,
                Address(row.SenderUserId, row.IsSystem, people),
                row.RecipientIds.Select(id => Address(id, false, people)).ToList(),
                row.DraftTo,
                row.Subject,
                MailRules.Preview(row.BodyStart),
                row.SortAt,
                row.IsRead,
                row.IsStarred,
                row.IsImportant,
                row.HasAttachments,
                row.TrashedAt != null,
                row.IsSystem))
            .ToList();

        return new PagedResult<MailListItemDto>(result, total, page, pageSize);
    }

    public async Task<IReadOnlyList<MailMessageDto>> GetMessagesAsync(IReadOnlyCollection<int> messageIds, CancellationToken cancellationToken = default)
    {
        var messages = await Set.AsNoTracking()
            .Where(message => messageIds.Contains(message.Id))
            .Select(message => new
            {
                message.Id,
                message.SenderUserId,
                message.IsSystem,
                message.Subject,
                message.Body,
                message.SentAt,
                message.TaskId,
                Recipients = message.Recipients.Select(recipient => new { recipient.UserId, recipient.Kind }).ToList(),
                Attachments = message.Attachments
                    .OrderBy(attachment => attachment.Id)
                    .Select(attachment => new MailAttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes))
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var people = await LoadPeopleAsync(
            messages.SelectMany(message => message.Recipients.Select(recipient => recipient.UserId).Append(message.SenderUserId ?? 0)),
            cancellationToken);

        return messages
            .Select(message => new MailMessageDto(
                message.Id,
                Address(message.SenderUserId, message.IsSystem, people),
                message.Recipients.Where(recipient => recipient.Kind == MailRecipientKind.To).Select(recipient => Address(recipient.UserId, false, people)).ToList(),
                message.Recipients.Where(recipient => recipient.Kind == MailRecipientKind.Cc).Select(recipient => Address(recipient.UserId, false, people)).ToList(),
                message.Subject,
                message.Body,
                message.SentAt,
                message.IsSystem,
                message.TaskId,
                message.Attachments))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetActiveUserIdsByEmailAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default)
    {
        // The Users query filter already hides deleted accounts.
        var users = await Db.Users
            .Where(user => user.IsActive && emails.Contains(user.Email.ToLower()))
            .Select(user => new { user.Id, Email = user.Email.ToLower() })
            .ToListAsync(cancellationToken);
        return users.GroupBy(user => user.Email).ToDictionary(group => group.Key, group => group.First().Id);
    }

    public async Task<IReadOnlyList<MailContactDto>> SearchContactsAsync(string term, int max, CancellationToken cancellationToken = default)
    {
        var pattern = "%" + EscapeLike(term) + "%";
        return await Db.Users
            .AsNoTracking()
            .Where(user => user.IsActive
                && (EF.Functions.Like(user.FullName, pattern, "\\") || EF.Functions.Like(user.Email, pattern, "\\")))
            .OrderBy(user => user.FullName)
            .Take(max)
            .Select(user => new MailContactDto(user.Id, user.FullName, user.Email))
            .ToListAsync(cancellationToken);
    }

    public void AddRecipient(MailRecipient recipient) => Db.MailRecipients.Add(recipient);

    public void AddMailboxItem(MailboxItem item) => Db.MailboxItems.Add(item);

    public void RemoveMailboxItem(MailboxItem item) => Db.MailboxItems.Remove(item);

    public void DiscardStagedMail()
    {
        foreach (var entry in Db.ChangeTracker.Entries()
                     .Where(entry => entry.State == EntityState.Added
                         && entry.Entity is MailMessage or MailRecipient or MailboxItem)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static IQueryable<MailboxItem> InView(IQueryable<MailboxItem> items, MailView view) => view switch
    {
        MailView.Inbox => items.Where(item => item.Folder == MailFolder.Inbox && item.TrashedAt == null),
        MailView.Sent => items.Where(item => item.Folder == MailFolder.Sent && item.TrashedAt == null),
        MailView.Drafts => items.Where(item => item.Folder == MailFolder.Drafts && item.TrashedAt == null),
        MailView.Starred => items.Where(item => item.IsStarred && item.TrashedAt == null && item.Folder != MailFolder.Drafts),
        MailView.Important => items.Where(item => item.IsImportant && item.TrashedAt == null && item.Folder != MailFolder.Drafts),
        MailView.Trash => items.Where(item => item.TrashedAt != null),
        _ => items.Where(_ => false)
    };

    private static string EscapeLike(string term) =>
        term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>Names and emails by user id, including deactivated or deleted users so old mail still reads well.</summary>
    private async Task<IReadOnlyDictionary<int, (string Name, string Email)>> LoadPeopleAsync(IEnumerable<int> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(id => id > 0).Distinct().ToList();
        return await Db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => (user.FullName, user.Email), cancellationToken);
    }

    private static MailAddressDto Address(int? userId, bool isSystem, IReadOnlyDictionary<int, (string Name, string Email)> people)
    {
        if (isSystem || userId is null)
        {
            return new MailAddressDto(null, MailRules.SystemSenderName, string.Empty);
        }

        return people.TryGetValue(userId.Value, out var person)
            ? new MailAddressDto(userId, person.Name, person.Email)
            : new MailAddressDto(userId, "Unknown user", string.Empty);
    }
}
