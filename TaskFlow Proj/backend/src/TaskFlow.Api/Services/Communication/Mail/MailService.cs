using System.Globalization;
using System.Text;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Common.Models;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Repositories.Communication.Mail;
using TaskFlow.Api.Repositories.Communication.Mail.Attachments;
using TaskFlow.Api.Services.Settings.ApplicationSettings;

namespace TaskFlow.Api.Services.Communication.Mail;

/// <summary>Internal mail between TaskFlow users. Every query starts from the caller's own mailbox items, so a mail
/// that is not in the caller's mailbox is simply not found. Sending writes the message, its recipients and every
/// participant's mailbox item in one transaction.</summary>
public class MailService : IMailService
{
    private readonly IMailRepository _mail;
    private readonly IMailAttachmentRepository _attachments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IAppClock _clock;

    public MailService(
        IMailRepository mail,
        IMailAttachmentRepository attachments,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IAppClock clock)
    {
        _mail = mail;
        _attachments = attachments;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _clock = clock;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- sending and drafts

    public async Task<MailItemDetailDto?> SendAsync(int callerId, SendMailRequest request, CancellationToken cancellationToken = default)
    {
        MailMessage? draft = null;
        MailboxItem? draftItem = null;
        MailMessage? parent = null;
        if (request.DraftId is { } draftId)
        {
            draftItem = await _mail.GetDraftForUpdateAsync(callerId, draftId, cancellationToken);
            if (draftItem?.Message is null || draftItem.Message.SentAt is not null)
            {
                return null;
            }

            draft = draftItem.Message;
        }
        else if (request.ParentItemId is { } parentItemId)
        {
            parent = await LoadParentAsync(callerId, parentItemId, request.Mode, cancellationToken);
            if (parent is null)
            {
                return null;
            }
        }

        var subject = (request.Subject ?? draft?.Subject ?? string.Empty).Trim();
        var body = request.Body ?? draft?.Body ?? string.Empty;
        if (subject.Length == 0 && string.IsNullOrWhiteSpace(body))
        {
            throw new MailValidationException("Body", "Write a subject or a message before sending.");
        }

        // A draft sent with only its id keeps the recipients and attachments it was saved with.
        var (toUsers, ccUsers) = await ResolveRecipientsAsync(
            request.To ?? SplitAddresses(draft?.DraftTo),
            request.Cc ?? SplitAddresses(draft?.DraftCc),
            cancellationToken);
        var attachmentIds = request.AttachmentIds;
        if (draft is not null && attachmentIds is null)
        {
            attachmentIds = await _attachments.GetIdsOnMessageAsync(draft.Id, cancellationToken);
        }

        // Every check happens before anything is written.
        var attachments = await PlanAttachmentsAsync(callerId, draft?.Id, attachmentIds, request.Mode == MailComposeMode.Forward ? parent : null, cancellationToken);
        var now = UtcNow;

        var sentItemId = await InTransactionAsync(async token =>
        {
            var message = draft ?? new MailMessage
            {
                ConversationId = parent?.ConversationId ?? Guid.NewGuid(),
                ParentMessageId = parent?.Id,
                SenderUserId = callerId,
                CreatedAt = now
            };
            message.Subject = subject;
            message.Body = body;
            message.DraftTo = null;
            message.DraftCc = null;
            message.UpdatedAt = now;
            message.SentAt = now;
            if (draft is null)
            {
                _mail.Add(message);
                await _unitOfWork.SaveChangesAsync(token);
            }

            ApplyAttachments(attachments, message.Id, callerId);

            foreach (var userId in toUsers)
            {
                _mail.AddRecipient(new MailRecipient { MessageId = message.Id, UserId = userId, Kind = MailRecipientKind.To });
            }

            foreach (var userId in ccUsers)
            {
                _mail.AddRecipient(new MailRecipient { MessageId = message.Id, UserId = userId, Kind = MailRecipientKind.Cc });
            }

            var sentItem = new MailboxItem { MessageId = message.Id, OwnerUserId = callerId, Folder = MailFolder.Sent, IsRead = true, SortAt = now };
            _mail.AddMailboxItem(sentItem);
            foreach (var userId in toUsers.Concat(ccUsers))
            {
                _mail.AddMailboxItem(new MailboxItem { MessageId = message.Id, OwnerUserId = userId, Folder = MailFolder.Inbox, SortAt = now });
            }

            if (draftItem is not null)
            {
                _mail.RemoveMailboxItem(draftItem);
            }

            await _unitOfWork.SaveChangesAsync(token);
            return sentItem.Id;
        }, cancellationToken);

        return await BuildDetailAsync(callerId, sentItemId, cancellationToken);
    }

    /// <summary>Runs a write in one transaction. Whatever happens, every loaded entity is forgotten afterwards, so a
    /// failed write never leaves half-written entities behind for the next operation in this request.</summary>
    private async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> write, CancellationToken cancellationToken)
    {
        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync(write, cancellationToken);
        }
        finally
        {
            _unitOfWork.DetachAll();
        }
    }

    public async Task<MailItemDetailDto?> CreateDraftAsync(int callerId, SaveDraftRequest request, CancellationToken cancellationToken = default)
    {
        MailMessage? parent = null;
        if (request.ParentItemId is { } parentItemId)
        {
            parent = await LoadParentAsync(callerId, parentItemId, request.Mode, cancellationToken);
            if (parent is null)
            {
                return null;
            }
        }

        var attachments = await PlanAttachmentsAsync(callerId, null, request.AttachmentIds, request.Mode == MailComposeMode.Forward ? parent : null, cancellationToken);
        var now = UtcNow;

        var itemId = await InTransactionAsync(async token =>
        {
            var draft = new MailMessage
            {
                ConversationId = parent?.ConversationId ?? Guid.NewGuid(),
                ParentMessageId = parent?.Id,
                SenderUserId = callerId,
                CreatedAt = now
            };
            ApplyDraftFields(draft, request, now);
            _mail.Add(draft);
            await _unitOfWork.SaveChangesAsync(token);

            ApplyAttachments(attachments, draft.Id, callerId);
            var item = new MailboxItem { MessageId = draft.Id, OwnerUserId = callerId, Folder = MailFolder.Drafts, IsRead = true, SortAt = now };
            _mail.AddMailboxItem(item);
            await _unitOfWork.SaveChangesAsync(token);
            return item.Id;
        }, cancellationToken);

        return await BuildDetailAsync(callerId, itemId, cancellationToken);
    }

    public async Task<MailItemDetailDto?> UpdateDraftAsync(int callerId, int draftId, SaveDraftRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _mail.GetDraftForUpdateAsync(callerId, draftId, cancellationToken);
        if (item?.Message is null || item.Message.SentAt is not null)
        {
            return null;
        }

        var attachments = await PlanAttachmentsAsync(callerId, item.MessageId, request.AttachmentIds, forwardFrom: null, cancellationToken);
        var now = UtcNow;

        await InTransactionAsync(async token =>
        {
            ApplyDraftFields(item.Message, request, now);
            item.SortAt = now;
            ApplyAttachments(attachments, item.MessageId, callerId);
            return await _unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);

        return await BuildDetailAsync(callerId, item.Id, cancellationToken);
    }

    private static void ApplyDraftFields(MailMessage draft, SaveDraftRequest request, DateTime now)
    {
        draft.Subject = (request.Subject ?? string.Empty).Trim();
        draft.Body = request.Body ?? string.Empty;
        draft.DraftTo = JoinAddresses(request.To);
        draft.DraftCc = JoinAddresses(request.Cc);
        draft.UpdatedAt = now;
    }

    private static IReadOnlyList<string> SplitAddresses(string? joined) =>
        (joined ?? string.Empty).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? JoinAddresses(IReadOnlyList<string>? addresses)
    {
        var cleaned = (addresses ?? []).Select(address => address?.Trim() ?? string.Empty).Where(address => address.Length > 0).ToList();
        return cleaned.Count == 0 ? null : string.Join(", ", cleaned);
    }

    /// <summary>The caller's parent item's message for a reply or forward, or null when the item is not the caller's.
    /// Replying to a system mail is refused.</summary>
    private async Task<MailMessage?> LoadParentAsync(int callerId, int parentItemId, MailComposeMode? mode, CancellationToken cancellationToken)
    {
        var parent = await _mail.GetMessageOfItemAsync(callerId, parentItemId, cancellationToken);
        if (parent is null)
        {
            return null;
        }

        if (parent.IsSystem && mode is MailComposeMode.Reply or MailComposeMode.ReplyAll)
        {
            throw new MailValidationException("Mode", "Mails from TaskFlow cannot be replied to. Forward it instead.");
        }

        return parent;
    }

    /// <summary>Active users for the To and Cc addresses (a user in both keeps To). Any address that is not an
    /// active TaskFlow user rejects the whole send.</summary>
    private async Task<(IReadOnlyList<int> To, IReadOnlyList<int> Cc)> ResolveRecipientsAsync(
        IReadOnlyList<string>? toAddresses,
        IReadOnlyList<string>? ccAddresses,
        CancellationToken cancellationToken)
    {
        var to = MailRules.NormalizeAddresses(toAddresses);
        var cc = MailRules.NormalizeAddresses(ccAddresses).Except(to).ToList();
        var all = to.Concat(cc).ToList();
        if (all.Count == 0)
        {
            throw new MailValidationException("To", "Add at least one recipient.");
        }

        if (all.Count > MailRules.MaxRecipients)
        {
            throw new MailValidationException("To", $"A mail can have at most {MailRules.MaxRecipients} recipients.");
        }

        var byEmail = await _mail.GetActiveUserIdsByEmailAsync(all, cancellationToken);

        var errors = new Dictionary<string, string[]>();
        var unknownTo = to.Where(address => !byEmail.ContainsKey(address)).ToList();
        var unknownCc = cc.Where(address => !byEmail.ContainsKey(address)).ToList();
        if (unknownTo.Count > 0)
        {
            errors["To"] = [$"Not an active TaskFlow user: {string.Join(", ", unknownTo)}."];
        }

        if (unknownCc.Count > 0)
        {
            errors["Cc"] = [$"Not an active TaskFlow user: {string.Join(", ", unknownCc)}."];
        }

        if (errors.Count > 0)
        {
            throw new MailValidationException(errors);
        }

        var toIds = to.Select(address => byEmail[address]).Distinct().ToList();
        var ccIds = cc.Select(address => byEmail[address]).Distinct().Except(toIds).ToList();
        return (toIds, ccIds);
    }

    /// <summary>The attachment changes for a message, decided and checked before anything is written.</summary>
    private sealed record AttachmentPlan(
        IReadOnlyList<MailAttachment> Chosen,
        IReadOnlyList<MailAttachment> Dropped,
        IReadOnlyList<MailAttachment> Forwarded);

    /// <summary>Checks that <paramref name="attachmentIds"/> are the caller's own uploads (unattached, or already on
    /// <paramref name="messageId"/>) and that they, plus copies of <paramref name="forwardFrom"/>'s files, stay
    /// within the count and size limits.</summary>
    private async Task<AttachmentPlan> PlanAttachmentsAsync(
        int callerId,
        int? messageId,
        IReadOnlyList<int>? attachmentIds,
        MailMessage? forwardFrom,
        CancellationToken cancellationToken)
    {
        var ids = (attachmentIds ?? []).Distinct().ToList();
        var chosen = await _attachments.GetOwnAvailableForUpdateAsync(callerId, ids, messageId, cancellationToken);
        if (chosen.Count != ids.Count)
        {
            throw new MailValidationException("AttachmentIds", "One or more attachments are not available.");
        }

        var forwarded = forwardFrom is null
            ? []
            : await _attachments.GetOnMessageAsync(forwardFrom.Id, cancellationToken);

        if (chosen.Count + forwarded.Count > MailRules.MaxAttachments)
        {
            throw new MailValidationException("AttachmentIds", $"A mail can have at most {MailRules.MaxAttachments} attachments.");
        }

        if (chosen.Sum(attachment => attachment.SizeBytes) + forwarded.Sum(attachment => attachment.SizeBytes) > MailRules.MaxTotalAttachmentBytes)
        {
            throw new MailValidationException("AttachmentIds", "Attachments on one mail can total at most 25 MB.");
        }

        var dropped = messageId is null
            ? []
            : await _attachments.GetOthersOnMessageForUpdateAsync(messageId.Value, ids, cancellationToken);

        return new AttachmentPlan(chosen, dropped, forwarded);
    }

    /// <summary>Applies a checked plan: attaches the chosen uploads, detaches the dropped ones and adds forwarded
    /// copies that share the original stored files.</summary>
    private void ApplyAttachments(AttachmentPlan plan, int messageId, int callerId)
    {
        foreach (var attachment in plan.Dropped)
        {
            attachment.MessageId = null;
        }

        foreach (var attachment in plan.Chosen)
        {
            attachment.MessageId = messageId;
        }

        foreach (var original in plan.Forwarded)
        {
            _attachments.Add(new MailAttachment
            {
                MessageId = messageId,
                UploadedByUserId = callerId,
                FileName = original.FileName,
                ContentType = original.ContentType,
                SizeBytes = original.SizeBytes,
                StorageKey = original.StorageKey,
                CreatedAt = UtcNow
            });
        }
    }

    // ---------------------------------------------------------------- folders, items and flags

    public Task<PagedResult<MailListItemDto>> ListAsync(int callerId, MailListQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MailRules.MaxPageSize);
        return _mail.ListAsync(callerId, query.Folder, query.Search, page, pageSize, cancellationToken);
    }

    public async Task<MailSummaryDto> GetSummaryAsync(int callerId, CancellationToken cancellationToken = default) =>
        new(
            await _mail.CountAsync(callerId, MailView.Inbox, unreadOnly: true, cancellationToken),
            await _mail.CountAsync(callerId, MailView.Starred, unreadOnly: true, cancellationToken),
            await _mail.CountAsync(callerId, MailView.Important, unreadOnly: true, cancellationToken),
            await _mail.CountAsync(callerId, MailView.Drafts, unreadOnly: false, cancellationToken));

    public Task<int> GetUnreadCountAsync(int callerId, CancellationToken cancellationToken = default) =>
        _mail.CountAsync(callerId, MailView.Inbox, unreadOnly: true, cancellationToken);

    public async Task<MailItemDetailDto?> OpenAsync(int callerId, int itemId, CancellationToken cancellationToken = default)
    {
        var item = await _mail.GetItemForUpdateAsync(callerId, itemId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        if (!item.IsRead)
        {
            item.IsRead = true;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await BuildDetailAsync(callerId, itemId, cancellationToken);
    }

    public async Task<bool> UpdateFlagsAsync(int callerId, int itemId, UpdateMailFlagsRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _mail.GetItemForUpdateAsync(callerId, itemId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.IsRead = request.IsRead ?? item.IsRead;
        item.IsStarred = request.IsStarred ?? item.IsStarred;
        item.IsImportant = request.IsImportant ?? item.IsImportant;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TrashAsync(int callerId, int itemId, CancellationToken cancellationToken = default)
    {
        var item = await _mail.GetItemForUpdateAsync(callerId, itemId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.TrashedAt ??= UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RestoreAsync(int callerId, int itemId, CancellationToken cancellationToken = default)
    {
        var item = await _mail.GetItemForUpdateAsync(callerId, itemId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.TrashedAt = null;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteForeverAsync(int callerId, int itemId, CancellationToken cancellationToken = default)
    {
        var item = await _mail.GetItemForUpdateAsync(callerId, itemId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        if (item.TrashedAt is null && item.Folder != MailFolder.Drafts)
        {
            throw new ConflictException("Move the mail to Trash before deleting it forever.");
        }

        item.PurgedAt = UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---------------------------------------------------------------- compose (reply, reply all, forward)

    public async Task<MailComposeDto?> GetComposeAsync(int callerId, int itemId, MailComposeMode mode, CancellationToken cancellationToken = default)
    {
        if (await _mail.GetMessageIdOfItemAsync(callerId, itemId, cancellationToken) is not { } messageId)
        {
            return null;
        }

        var message = (await _mail.GetMessagesAsync([messageId], cancellationToken)).Single();
        if (message.IsSystem && mode is MailComposeMode.Reply or MailComposeMode.ReplyAll)
        {
            throw new MailValidationException("Mode", "Mails from TaskFlow cannot be replied to. Forward it instead.");
        }

        var to = new List<string>();
        var cc = new List<string>();
        var senderIsMe = message.From.UserId == callerId;
        switch (mode)
        {
            case MailComposeMode.Reply:
                to.AddRange(senderIsMe ? message.To.Select(person => person.Email) : [message.From.Email]);
                break;
            case MailComposeMode.ReplyAll:
                if (!senderIsMe)
                {
                    to.Add(message.From.Email);
                }

                to.AddRange(message.To.Where(person => person.UserId != callerId).Select(person => person.Email));
                cc.AddRange(message.Cc.Where(person => person.UserId != callerId).Select(person => person.Email));
                break;
        }

        var when = await LocalTimeAsync(message.SentAt, cancellationToken);
        string subject;
        string body;
        if (mode == MailComposeMode.Forward)
        {
            subject = MailRules.WithPrefix(MailRules.ForwardPrefix, message.Subject);
            body = new StringBuilder()
                .AppendLine()
                .AppendLine()
                .AppendLine("---------- Forwarded message ----------")
                .Append("From: ").AppendLine(Describe(message.From))
                .Append("Date: ").AppendLine(when)
                .Append("Subject: ").AppendLine(message.Subject)
                .Append("To: ").AppendLine(string.Join(", ", message.To.Select(Describe)))
                .AppendLine()
                .Append(message.Body)
                .ToString();
        }
        else
        {
            subject = MailRules.WithPrefix(MailRules.ReplyPrefix, message.Subject);
            var quoted = string.Join(Environment.NewLine, message.Body.Split('\n').Select(line => "> " + line.TrimEnd('\r')));
            body = $"{Environment.NewLine}{Environment.NewLine}On {when}, {message.From.Name} wrote:{Environment.NewLine}{quoted}";
        }

        if (body.Length > MailRules.MaxBodyLength)
        {
            body = body[..MailRules.MaxBodyLength];
        }

        return new MailComposeDto(
            mode,
            itemId,
            to.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            cc.Distinct(StringComparer.OrdinalIgnoreCase).Except(to, StringComparer.OrdinalIgnoreCase).ToList(),
            subject,
            body,
            mode == MailComposeMode.Forward ? message.Attachments : []);
    }

    private static string Describe(MailAddressDto person) =>
        string.IsNullOrEmpty(person.Email) ? person.Name : $"{person.Name} <{person.Email}>";

    private async Task<string> LocalTimeAsync(DateTime? utc, CancellationToken cancellationToken)
    {
        if (utc is null)
        {
            return string.Empty;
        }

        var zone = await _clock.GetTimeZoneAsync(cancellationToken);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), zone);
        return local.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
    }

    // ---------------------------------------------------------------- contacts

    public async Task<IReadOnlyList<MailContactDto>> GetContactsAsync(string? search, CancellationToken cancellationToken = default)
    {
        var term = search?.Trim();
        return string.IsNullOrEmpty(term)
            ? []
            : await _mail.SearchContactsAsync(term, MailRules.MaxContacts, cancellationToken);
    }

    // ---------------------------------------------------------------- building details

    private async Task<MailItemDetailDto?> BuildDetailAsync(int callerId, int itemId, CancellationToken cancellationToken)
    {
        var item = await _mail.GetItemHeaderAsync(callerId, itemId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        // The conversation as the caller sees it: sent or received mails in their mailbox, never their drafts.
        var threadIds = item.Folder == MailFolder.Drafts
            ? [item.MessageId]
            : await _mail.GetConversationMessageIdsAsync(callerId, item.ConversationId, cancellationToken);

        var messages = await _mail.GetMessagesAsync(threadIds, cancellationToken);
        var thread = messages.OrderBy(message => message.SentAt ?? DateTime.MaxValue).ThenBy(message => message.MessageId).ToList();

        return new MailItemDetailDto(
            item.Id,
            item.Folder,
            item.IsRead,
            item.IsStarred,
            item.IsImportant,
            item.IsTrashed,
            item.DraftTo,
            item.DraftCc,
            thread.Single(message => message.MessageId == item.MessageId),
            thread);
    }
}
