using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Common.Exceptions;

namespace TaskFlow.Api.Services.Communication.Mail.Attachments;

/// <summary>Uploading, downloading and removing mail attachments. Downloads are allowed only to the uploader or
/// to someone whose mailbox holds the mail; anything else is not found.</summary>
public interface IMailAttachmentService
{
    /// <summary>Stores an upload for the caller. Throws <see cref="MailValidationException"/> for an empty, too
    /// large or blocked file.</summary>
    Task<MailAttachmentDto> UploadAsync(int callerId, Stream content, string? fileName, CancellationToken cancellationToken = default);

    Task<MailAttachmentDownload?> OpenAsync(int callerId, int attachmentId, CancellationToken cancellationToken = default);

    /// <summary>Removes the caller's upload that is not yet on a sent mail or draft.</summary>
    Task<bool> DeleteAsync(int callerId, int attachmentId, CancellationToken cancellationToken = default);
}
