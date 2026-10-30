namespace TaskFlow.Application.Mail;

/// <summary>An attachment ready to stream to the caller.</summary>
public sealed record MailAttachmentDownload(Stream Content, string FileName, string ContentType);

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

/// <summary>Where attachment bytes live. Files are written once under a generated key and never changed.</summary>
public interface IMailFileStore
{
    /// <summary>Writes the stream under a new key; returns the key and the number of bytes written. Throws
    /// <see cref="MailValidationException"/> when the content exceeds <paramref name="maxBytes"/>, leaving nothing
    /// behind.</summary>
    Task<(Guid Key, long Size)> SaveAsync(Stream content, long maxBytes, CancellationToken cancellationToken = default);

    Stream? OpenRead(Guid key);
}
