using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Mail;
using TaskFlow.Application.Settings;
using TaskFlow.Application.Tasks;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Internal mail between TaskFlow users. Every query starts from the caller's own mailbox items, so a mail
/// that is not in the caller's mailbox is simply not found. Sending writes the message, its recipients and every
/// participant's mailbox item in one transaction.</summary>
public class MailService : IMailService
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IAppClock _clock;

    public MailService(AppDbContext dbContext, TimeProvider timeProvider, IAppClock clock)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _clock = clock;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>The caller's items that have not been deleted forever.</summary>
    private IQueryable<MailboxItem> Mine(int callerId) =>
        _dbContext.MailboxItems.Where(item => item.OwnerUserId == callerId && item.PurgedAt == null);

    // ---------------------------------------------------------------- sending and drafts

    public async Task<MailItemDetailDto?> SendAsync(int callerId, SendMailRequest request, CancellationToken cancellationToken = default)
    {
        MailMessage? draft = null;
        MailboxItem? draftItem = null;
        MailMessage? parent = null;
        if (request.DraftId is { } draftId)
        {
            draftItem = await Mine(callerId)
                .Include(item => item.Message)
                .FirstOrDefaultAsync(item => item.MessageId == draftId && item.Folder == MailFolder.Drafts, cancellationToken);
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
            attachmentIds = await _dbContext.MailAttachments
                .Where(attachment => attachment.MessageId == draft.Id)
                .Select(attachment => attachment.Id)
                .ToListAsync(cancellationToken);
        }

        // Every check happens before anything is written.
        var attachments = await PlanAttachmentsAsync(callerId, draft?.Id, attachmentIds, request.Mode == MailComposeMode.Forward ? parent : null, cancellationToken);
        var now = UtcNow;

        var sentItemId = await InTransactionAsync(async () =>
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
                _dbContext.MailMessages.Add(message);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            ApplyAttachments(attachments, message.Id, callerId);

            foreach (var userId in toUsers)
            {
                _dbContext.MailRecipients.Add(new MailRecipient { MessageId = message.Id, UserId = userId, Kind = MailRecipientKind.To });
            }

            foreach (var userId in ccUsers)
            {
                _dbContext.MailRecipients.Add(new MailRecipient { MessageId = message.Id, UserId = userId, Kind = MailRecipientKind.Cc });
            }

            var sentItem = new MailboxItem { MessageId = message.Id, OwnerUserId = callerId, Folder = MailFolder.Sent, IsRead = true, SortAt = now };
            _dbContext.MailboxItems.Add(sentItem);
            foreach (var userId in toUsers.Concat(ccUsers))
            {
                _dbContext.MailboxItems.Add(new MailboxItem { MessageId = message.Id, OwnerUserId = userId, Folder = MailFolder.Inbox, SortAt = now });
            }

            if (draftItem is not null)
            {
                _dbContext.MailboxItems.Remove(draftItem);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return sentItem.Id;
        }, cancellationToken);

        return await BuildDetailAsync(callerId, sentItemId, cancellationToken);
    }

    /// <summary>Runs a write in one transaction. Whatever happens, the change tracker is cleared afterwards, so a
    /// failed write never leaves half-written entities behind for the next operation on this context.</summary>
    private async Task<T> InTransactionAsync<T>(Func<Task<T>> write, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var result = await write();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        finally
        {
            _dbContext.ChangeTracker.Clear();
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

        var itemId = await InTransactionAsync(async () =>
        {
            var draft = new MailMessage
            {
                ConversationId = parent?.ConversationId ?? Guid.NewGuid(),
                ParentMessageId = parent?.Id,
                SenderUserId = callerId,
                CreatedAt = now
            };
            ApplyDraftFields(draft, request, now);
            _dbContext.MailMessages.Add(draft);
            await _dbContext.SaveChangesAsync(cancellationToken);

            ApplyAttachments(attachments, draft.Id, callerId);
            var item = new MailboxItem { MessageId = draft.Id, OwnerUserId = callerId, Folder = MailFolder.Drafts, IsRead = true, SortAt = now };
            _dbContext.MailboxItems.Add(item);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return item.Id;
        }, cancellationToken);

        return await BuildDetailAsync(callerId, itemId, cancellationToken);
    }

    public async Task<MailItemDetailDto?> UpdateDraftAsync(int callerId, int draftId, SaveDraftRequest request, CancellationToken cancellationToken = default)
    {
        var item = await Mine(callerId)
            .Include(mailboxItem => mailboxItem.Message)
            .FirstOrDefaultAsync(mailboxItem => mailboxItem.MessageId == draftId && mailboxItem.Folder == MailFolder.Drafts, cancellationToken);
        if (item?.Message is null || item.Message.SentAt is not null)
        {
            return null;
        }

        var attachments = await PlanAttachmentsAsync(callerId, item.MessageId, request.AttachmentIds, forwardFrom: null, cancellationToken);
        var now = UtcNow;

        await InTransactionAsync(async () =>
        {
            ApplyDraftFields(item.Message, request, now);
            item.SortAt = now;
            ApplyAttachments(attachments, item.MessageId, callerId);
            return await _dbContext.SaveChangesAsync(cancellationToken);
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
        var parent = await Mine(callerId)
            .Where(item => item.Id == parentItemId && item.Folder != MailFolder.Drafts)
            .Select(item => item.Message)
            .FirstOrDefaultAsync(cancellationToken);
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

        // The Users query filter already hides deleted accounts.
        var users = await _dbContext.Users
            .Where(user => user.IsActive && all.Contains(user.Email.ToLower()))
            .Select(user => new { user.Id, Email = user.Email.ToLower() })
            .ToListAsync(cancellationToken);
        var byEmail = users.GroupBy(user => user.Email).ToDictionary(group => group.Key, group => group.First().Id);

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
        var chosen = await _dbContext.MailAttachments
            .Where(attachment => ids.Contains(attachment.Id)
                && attachment.UploadedByUserId == callerId
                && (attachment.MessageId == null || attachment.MessageId == messageId))
            .ToListAsync(cancellationToken);
        if (chosen.Count != ids.Count)
        {
            throw new MailValidationException("AttachmentIds", "One or more attachments are not available.");
        }

        var forwarded = forwardFrom is null
            ? []
            : await _dbContext.MailAttachments.AsNoTracking().Where(attachment => attachment.MessageId == forwardFrom.Id).ToListAsync(cancellationToken);

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
            : await _dbContext.MailAttachments
                .Where(attachment => attachment.MessageId == messageId && !ids.Contains(attachment.Id))
                .ToListAsync(cancellationToken);

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
            _dbContext.MailAttachments.Add(new MailAttachment
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

    public async Task<PagedResult<MailListItemDto>> ListAsync(int callerId, MailListQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MailRules.MaxPageSize);
        var items = InView(Mine(callerId), query.Folder);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + EscapeLike(query.Search.Trim()) + "%";
            items = items.Where(item =>
                EF.Functions.Like(item.Message!.Subject, pattern, "\\")
                || EF.Functions.Like(item.Message!.Body, pattern, "\\")
                || (item.Message!.DraftTo != null && EF.Functions.Like(item.Message!.DraftTo, pattern, "\\"))
                || (item.Message!.IsSystem && EF.Functions.Like(MailRules.SystemSenderName, pattern, "\\"))
                || _dbContext.Users.IgnoreQueryFilters().Any(user => user.Id == item.Message!.SenderUserId
                    && (EF.Functions.Like(user.FullName, pattern, "\\") || EF.Functions.Like(user.Email, pattern, "\\")))
                || _dbContext.MailRecipients.Any(recipient => recipient.MessageId == item.MessageId
                    && _dbContext.Users.IgnoreQueryFilters().Any(user => user.Id == recipient.UserId
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

    public async Task<MailSummaryDto> GetSummaryAsync(int callerId, CancellationToken cancellationToken = default)
    {
        var mine = Mine(callerId);
        return new MailSummaryDto(
            await InView(mine, MailView.Inbox).CountAsync(item => !item.IsRead, cancellationToken),
            await InView(mine, MailView.Starred).CountAsync(item => !item.IsRead, cancellationToken),
            await InView(mine, MailView.Important).CountAsync(item => !item.IsRead, cancellationToken),
            await InView(mine, MailView.Drafts).CountAsync(cancellationToken));
    }

    public Task<int> GetUnreadCountAsync(int callerId, CancellationToken cancellationToken = default) =>
        InView(Mine(callerId), MailView.Inbox).CountAsync(item => !item.IsRead, cancellationToken);

    public async Task<MailItemDetailDto?> OpenAsync(int callerId, int itemId, CancellationToken cancellationToken = default)
    {
        var item = await Mine(callerId).FirstOrDefaultAsync(mailboxItem => mailboxItem.Id == itemId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        if (!item.IsRead)
        {
            item.IsRead = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return await BuildDetailAsync(callerId, itemId, cancellationToken);
    }

    public async Task<bool> UpdateFlagsAsync(int callerId, int itemId, UpdateMailFlagsRequest request, CancellationToken cancellationToken = default)
    {
        var item = await Mine(callerId).FirstOrDefaultAsync(mailboxItem => mailboxItem.Id == itemId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.IsRead = request.IsRead ?? item.IsRead;
        item.IsStarred = request.IsStarred ?? item.IsStarred;
        item.IsImportant = request.IsImportant ?? item.IsImportant;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TrashAsync(int callerId, int itemId, CancellationToken cancellationToken = default)
    {
        var item = await Mine(callerId).FirstOrDefaultAsync(mailboxItem => mailboxItem.Id == itemId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.TrashedAt ??= UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RestoreAsync(int callerId, int itemId, CancellationToken cancellationToken = default)
    {
        var item = await Mine(callerId).FirstOrDefaultAsync(mailboxItem => mailboxItem.Id == itemId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.TrashedAt = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteForeverAsync(int callerId, int itemId, CancellationToken cancellationToken = default)
    {
        var item = await Mine(callerId).FirstOrDefaultAsync(mailboxItem => mailboxItem.Id == itemId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        if (item.TrashedAt is null && item.Folder != MailFolder.Drafts)
        {
            throw new InvalidOperationException("Move the mail to Trash before deleting it forever.");
        }

        item.PurgedAt = UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---------------------------------------------------------------- compose (reply, reply all, forward)

    public async Task<MailComposeDto?> GetComposeAsync(int callerId, int itemId, MailComposeMode mode, CancellationToken cancellationToken = default)
    {
        var item = await Mine(callerId)
            .Where(mailboxItem => mailboxItem.Id == itemId && mailboxItem.Folder != MailFolder.Drafts)
            .Select(mailboxItem => new { mailboxItem.Id, mailboxItem.MessageId })
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return null;
        }

        var message = (await LoadMessagesAsync([item.MessageId], cancellationToken)).Single();
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
            item.Id,
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
        if (string.IsNullOrEmpty(term))
        {
            return [];
        }

        var pattern = "%" + EscapeLike(term) + "%";
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive
                && (EF.Functions.Like(user.FullName, pattern, "\\") || EF.Functions.Like(user.Email, pattern, "\\")))
            .OrderBy(user => user.FullName)
            .Take(MailRules.MaxContacts)
            .Select(user => new MailContactDto(user.Id, user.FullName, user.Email))
            .ToListAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- building details

    private async Task<MailItemDetailDto?> BuildDetailAsync(int callerId, int itemId, CancellationToken cancellationToken)
    {
        var item = await Mine(callerId)
            .AsNoTracking()
            .Where(mailboxItem => mailboxItem.Id == itemId)
            .Select(mailboxItem => new
            {
                mailboxItem.Id,
                mailboxItem.MessageId,
                mailboxItem.Folder,
                mailboxItem.IsRead,
                mailboxItem.IsStarred,
                mailboxItem.IsImportant,
                mailboxItem.TrashedAt,
                mailboxItem.Message!.ConversationId,
                mailboxItem.Message.DraftTo,
                mailboxItem.Message.DraftCc
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return null;
        }

        // The conversation as the caller sees it: sent or received mails in their mailbox, never their drafts.
        var threadIds = item.Folder == MailFolder.Drafts
            ? [item.MessageId]
            : await Mine(callerId)
                .Where(mailboxItem => mailboxItem.Folder != MailFolder.Drafts && mailboxItem.Message!.ConversationId == item.ConversationId)
                .Select(mailboxItem => mailboxItem.MessageId)
                .Distinct()
                .ToListAsync(cancellationToken);

        var messages = await LoadMessagesAsync(threadIds, cancellationToken);
        var thread = messages.OrderBy(message => message.SentAt ?? DateTime.MaxValue).ThenBy(message => message.MessageId).ToList();

        return new MailItemDetailDto(
            item.Id,
            item.Folder,
            item.IsRead,
            item.IsStarred,
            item.IsImportant,
            item.TrashedAt != null,
            item.DraftTo,
            item.DraftCc,
            thread.Single(message => message.MessageId == item.MessageId),
            thread);
    }

    private async Task<IReadOnlyList<MailMessageDto>> LoadMessagesAsync(IReadOnlyCollection<int> messageIds, CancellationToken cancellationToken)
    {
        var messages = await _dbContext.MailMessages
            .AsNoTracking()
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

    /// <summary>Names and emails by user id, including deactivated or deleted users so old mail still reads well.</summary>
    private async Task<IReadOnlyDictionary<int, (string Name, string Email)>> LoadPeopleAsync(IEnumerable<int> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(id => id > 0).Distinct().ToList();
        return await _dbContext.Users
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
