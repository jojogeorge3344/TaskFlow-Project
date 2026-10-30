using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Interfaces.Communication.Mail.Attachments;

namespace TaskFlow.Api.Repositories.Communication.Mail.Attachments;

public class MailAttachmentRepository : BaseRepository<MailAttachment>, IMailAttachmentRepository
{
    public MailAttachmentRepository(AppDbContext db)
        : base(db)
    {
    }

    public Task<List<int>> GetIdsOnMessageAsync(int messageId, CancellationToken cancellationToken = default) =>
        Set.Where(attachment => attachment.MessageId == messageId).Select(attachment => attachment.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MailAttachment>> GetOwnAvailableForUpdateAsync(
        int uploaderId,
        IReadOnlyCollection<int> ids,
        int? messageId,
        CancellationToken cancellationToken = default) =>
        await Set.Where(attachment => ids.Contains(attachment.Id)
                && attachment.UploadedByUserId == uploaderId
                && (attachment.MessageId == null || attachment.MessageId == messageId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MailAttachment>> GetOnMessageAsync(int messageId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(attachment => attachment.MessageId == messageId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MailAttachment>> GetOthersOnMessageForUpdateAsync(
        int messageId,
        IReadOnlyCollection<int> keepIds,
        CancellationToken cancellationToken = default) =>
        await Set.Where(attachment => attachment.MessageId == messageId && !keepIds.Contains(attachment.Id)).ToListAsync(cancellationToken);

    public async Task<PendingUploads> GetPendingUploadsAsync(int uploaderId, CancellationToken cancellationToken = default)
    {
        var pending = await Set
            .Where(file => file.UploadedByUserId == uploaderId && file.MessageId == null)
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), Bytes = group.Sum(file => file.SizeBytes) })
            .FirstOrDefaultAsync(cancellationToken);
        return new PendingUploads(pending?.Count ?? 0, pending?.Bytes ?? 0);
    }

    public Task<AttachmentFile?> GetDownloadableAsync(int callerId, int attachmentId, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking()
            .Where(file => file.Id == attachmentId
                && (file.UploadedByUserId == callerId
                    || (file.MessageId != null && Db.MailboxItems.Any(item =>
                        item.MessageId == file.MessageId && item.OwnerUserId == callerId && item.PurgedAt == null))))
            .Select(file => new AttachmentFile(file.FileName, file.ContentType, file.StorageKey))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<MailAttachment?> GetOwnPendingForUpdateAsync(int uploaderId, int attachmentId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(file => file.Id == attachmentId && file.UploadedByUserId == uploaderId && file.MessageId == null, cancellationToken);
}
