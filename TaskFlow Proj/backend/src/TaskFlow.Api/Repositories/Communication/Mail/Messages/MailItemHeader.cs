using TaskFlow.Api.Models.Enums;

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
