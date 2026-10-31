using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Communication.Mail.Attachments;

/// <summary>Attachment records. The bytes live in the mail file store under <see cref="MailAttachment.StorageKey"/>.</summary>
public interface IMailAttachmentRepository : IBaseRepository<MailAttachment>
{
    Task<List<int>> GetIdsOnMessageAsync(int messageId, CancellationToken cancellationToken = default);

    /// <summary>The tracked uploads among <paramref name="ids"/> that the user made and that are unattached or already on
    /// <paramref name="messageId"/>.</summary>
    Task<IReadOnlyList<MailAttachment>> GetOwnAvailableForUpdateAsync(
        int uploaderId,
        IReadOnlyCollection<int> ids,
        int? messageId,
        CancellationToken cancellationToken = default);

    /// <summary>The attachments on a message, read-only.</summary>
    Task<IReadOnlyList<MailAttachment>> GetOnMessageAsync(int messageId, CancellationToken cancellationToken = default);

    /// <summary>The tracked attachments on a message other than <paramref name="keepIds"/>.</summary>
    Task<IReadOnlyList<MailAttachment>> GetOthersOnMessageForUpdateAsync(
        int messageId,
        IReadOnlyCollection<int> keepIds,
        CancellationToken cancellationToken = default);

    Task<PendingUploads> GetPendingUploadsAsync(int uploaderId, CancellationToken cancellationToken = default);

    /// <summary>The file of an attachment the caller uploaded, or one on a mail in the caller's mailbox.</summary>
    Task<AttachmentFile?> GetDownloadableAsync(int callerId, int attachmentId, CancellationToken cancellationToken = default);

    /// <summary>The caller's tracked upload that is not on any mail yet.</summary>
    Task<MailAttachment?> GetOwnPendingForUpdateAsync(int uploaderId, int attachmentId, CancellationToken cancellationToken = default);
}
