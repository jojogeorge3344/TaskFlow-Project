using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Mail;

/// <summary>The folders a user can open. Inbox, Sent and Drafts are item folders; Starred, Important and Trash
/// are views over the user's items.</summary>
public enum MailView
{
    Inbox,
    Starred,
    Important,
    Sent,
    Drafts,
    Trash
}

public enum MailComposeMode
{
    Reply,
    ReplyAll,
    Forward
}

/// <summary>A person on a mail. The TaskFlow system sender has no user id and an empty email.</summary>
public record MailAddressDto(int? UserId, string Name, string Email);

public record MailAttachmentDto(int Id, string FileName, string ContentType, long SizeBytes);

/// <summary>One row of a folder list.</summary>
public record MailListItemDto(
    int ItemId,
    int MessageId,
    MailFolder Folder,
    MailAddressDto From,
    IReadOnlyList<MailAddressDto> To,
    string? DraftTo,
    string Subject,
    string Preview,
    DateTime Date,
    bool IsRead,
    bool IsStarred,
    bool IsImportant,
    bool HasAttachments,
    bool IsTrashed,
    bool IsSystem);

/// <summary>A mail's full content as one participant sees it.</summary>
public record MailMessageDto(
    int MessageId,
    MailAddressDto From,
    IReadOnlyList<MailAddressDto> To,
    IReadOnlyList<MailAddressDto> Cc,
    string Subject,
    string Body,
    DateTime? SentAt,
    bool IsSystem,
    int? TaskId,
    IReadOnlyList<MailAttachmentDto> Attachments);

/// <summary>An opened mailbox item: its state, the mail and the rest of its conversation in the caller's mailbox
/// (oldest first, including this mail). Drafts carry their raw recipient text.</summary>
public record MailItemDetailDto(
    int ItemId,
    MailFolder Folder,
    bool IsRead,
    bool IsStarred,
    bool IsImportant,
    bool IsTrashed,
    string? DraftTo,
    string? DraftCc,
    MailMessageDto Message,
    IReadOnlyList<MailMessageDto> Thread);

/// <summary>Counts for the folder rail.</summary>
public record MailSummaryDto(int InboxUnread, int StarredUnread, int ImportantUnread, int Drafts);

public record MailUnreadCountDto(int Count);

public record MailContactDto(int Id, string FullName, string Email);

/// <summary>Prefilled fields for a reply, reply all or forward. Forwarded attachments are copied when sent.</summary>
public record MailComposeDto(
    MailComposeMode Mode,
    int ParentItemId,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    string Body,
    IReadOnlyList<MailAttachmentDto> ForwardedAttachments);

/// <summary>Send a new mail, a reply or forward (<see cref="ParentItemId"/> + <see cref="Mode"/>), or a saved draft
/// (<see cref="DraftId"/>). Recipients are TaskFlow account email addresses.</summary>
public record SendMailRequest(
    IReadOnlyList<string>? To,
    IReadOnlyList<string>? Cc,
    string? Subject,
    string? Body,
    IReadOnlyList<int>? AttachmentIds,
    int? DraftId = null,
    int? ParentItemId = null,
    MailComposeMode? Mode = null);

/// <summary>Create or update a draft. Recipients may be incomplete or not yet valid.</summary>
public record SaveDraftRequest(
    IReadOnlyList<string>? To,
    IReadOnlyList<string>? Cc,
    string? Subject,
    string? Body,
    IReadOnlyList<int>? AttachmentIds,
    int? ParentItemId = null,
    MailComposeMode? Mode = null);

/// <summary>Personal flags to change; null leaves a flag as it is.</summary>
public record UpdateMailFlagsRequest(bool? IsRead, bool? IsStarred, bool? IsImportant);

public record MailListQuery(MailView Folder, string? Search, int Page = 1, int PageSize = MailRules.DefaultPageSize);
