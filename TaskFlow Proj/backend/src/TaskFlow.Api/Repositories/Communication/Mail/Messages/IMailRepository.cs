using TaskFlow.Api.Common.Models;
using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Communication.Mail.Messages;

/// <summary>One mailbox item as its owner sees it, with what its message's conversation needs.</summary>
public record MailItemHeader(
    int Id,
    int MessageId,
    MailFolder Folder,
    bool IsRead,
    bool IsStarred,
    bool IsImportant,
    bool IsTrashed,
    Guid ConversationId,
    string? DraftTo,
    string? DraftCc);

/// <summary>Mail messages, their recipients and every user's mailbox items. Every mailbox query starts from the owner's
/// own items that have not been deleted forever, so a mail outside the owner's mailbox is simply not found.</summary>
public interface IMailRepository : IBaseRepository<MailMessage>
{
    /// <summary>The owner's tracked draft item for this draft message, with the message.</summary>
    Task<MailboxItem?> GetDraftForUpdateAsync(int ownerId, int draftMessageId, CancellationToken cancellationToken = default);

    /// <summary>The message of the owner's item, unless the item is a draft.</summary>
    Task<MailMessage?> GetMessageOfItemAsync(int ownerId, int itemId, CancellationToken cancellationToken = default);

    /// <summary>The message id of the owner's item, unless the item is a draft.</summary>
    Task<int?> GetMessageIdOfItemAsync(int ownerId, int itemId, CancellationToken cancellationToken = default);

    /// <summary>The owner's tracked item, for changing its flags or folder.</summary>
    Task<MailboxItem?> GetItemForUpdateAsync(int ownerId, int itemId, CancellationToken cancellationToken = default);

    Task<MailItemHeader?> GetItemHeaderAsync(int ownerId, int itemId, CancellationToken cancellationToken = default);

    /// <summary>The messages of a conversation that are in the owner's mailbox as sent or received mail (never drafts).</summary>
    Task<IReadOnlyList<int>> GetConversationMessageIdsAsync(int ownerId, Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>How many of the owner's items are in the view, optionally only unread ones.</summary>
    Task<int> CountAsync(int ownerId, MailView view, bool unreadOnly, CancellationToken cancellationToken = default);

    /// <summary>One page of the owner's items in the view, newest first, matching the search in the subject, body,
    /// draft recipients, sender or recipients.</summary>
    Task<PagedResult<MailListItemDto>> ListAsync(
        int ownerId,
        MailView view,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>The messages with their sender, recipients and attachments. Senders and recipients include deactivated or
    /// deleted users, so old mail still reads well.</summary>
    Task<IReadOnlyList<MailMessageDto>> GetMessagesAsync(IReadOnlyCollection<int> messageIds, CancellationToken cancellationToken = default);

    /// <summary>Active users whose email (lower case) is one of these, keyed by that email.</summary>
    Task<IReadOnlyDictionary<string, int>> GetActiveUserIdsByEmailAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default);

    /// <summary>Active users whose name or email contains the term, by name.</summary>
    Task<IReadOnlyList<MailContactDto>> SearchContactsAsync(string term, int max, CancellationToken cancellationToken = default);

    void AddRecipient(MailRecipient recipient);

    void AddMailboxItem(MailboxItem item);

    void RemoveMailboxItem(MailboxItem item);

    /// <summary>Un-stages mail messages, recipients and mailbox items this request added but has not saved, leaving every
    /// other change in place.</summary>
    void DiscardStagedMail();
}
