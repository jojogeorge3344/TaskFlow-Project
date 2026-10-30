using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Interfaces.Communication.Mail.Attachments;

/// <summary>How many uploads a user has waiting to be attached, and their total size.</summary>
public record PendingUploads(int Count, long Bytes);

/// <summary>What a download needs from an attachment record.</summary>
public record AttachmentFile(string FileName, string ContentType, Guid StorageKey);

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
