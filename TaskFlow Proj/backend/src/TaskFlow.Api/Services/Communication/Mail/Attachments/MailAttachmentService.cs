using TaskFlow.Api.Integrations.Storage;
using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Communication.Mail.Attachments;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Services.Communication.Mail.Attachments;

/// <summary>Validates, stores and serves mail attachments. The content type is chosen from the file extension
/// against a small allowlist, never taken from the client, and everything else is served as a plain download.</summary>
public class MailAttachmentService : IMailAttachmentService
{
    private const string Fallback = "application/octet-stream";

    private static readonly IReadOnlyDictionary<string, string> SafeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".zip"] = "application/zip"
    };

    private readonly IMailAttachmentRepository _attachments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMailFileStore _store;
    private readonly TimeProvider _timeProvider;

    public MailAttachmentService(IMailAttachmentRepository attachments, IUnitOfWork unitOfWork, IMailFileStore store, TimeProvider timeProvider)
    {
        _attachments = attachments;
        _unitOfWork = unitOfWork;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<MailAttachmentDto> UploadAsync(int callerId, Stream content, string? fileName, CancellationToken cancellationToken = default)
    {
        var name = MailRules.SanitizeFileName(fileName);
        var extension = Path.GetExtension(name);
        if (MailRules.BlockedExtensions.Contains(extension))
        {
            throw new MailValidationException("File", $"Files of type {extension} are not allowed.");
        }

        // Uploads that never reach a mail are kept, so cap how many a user can leave lying around.
        var pending = await _attachments.GetPendingUploadsAsync(callerId, cancellationToken);
        if (pending.Count >= MailRules.MaxPendingUploads)
        {
            throw new MailValidationException("File", $"You have {pending.Count} files waiting to be attached. Send or discard a mail before uploading more.");
        }

        var room = MailRules.MaxPendingUploadBytes - pending.Bytes;
        if (room <= 0)
        {
            throw new MailValidationException("File", "Your files waiting to be attached already total 100 MB. Send or discard a mail before uploading more.");
        }

        Guid key;
        long size;
        try
        {
            (key, size) = await _store.SaveAsync(content, Math.Min(MailRules.MaxAttachmentBytes, room), cancellationToken);
        }
        catch (MailValidationException) when (room < MailRules.MaxAttachmentBytes)
        {
            throw new MailValidationException("File", "This file would take your files waiting to be attached past 100 MB. Send or discard a mail first.");
        }
        if (size == 0)
        {
            throw new MailValidationException("File", "The file is empty.");
        }

        var attachment = new MailAttachment
        {
            UploadedByUserId = callerId,
            FileName = name,
            ContentType = SafeTypes.TryGetValue(extension, out var type) ? type : Fallback,
            SizeBytes = size,
            StorageKey = key,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _attachments.Add(attachment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MailAttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes);
    }

    public async Task<MailAttachmentDownload?> OpenAsync(int callerId, int attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _attachments.GetDownloadableAsync(callerId, attachmentId, cancellationToken);
        if (attachment is null)
        {
            return null;
        }

        var stream = _store.OpenRead(attachment.StorageKey);
        return stream is null ? null : new MailAttachmentDownload(stream, attachment.FileName, attachment.ContentType);
    }

    public async Task<bool> DeleteAsync(int callerId, int attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _attachments.GetOwnPendingForUpdateAsync(callerId, attachmentId, cancellationToken);
        if (attachment is null)
        {
            return false;
        }

        // The stored file may be shared by a forwarded copy, so only the record is removed.
        _attachments.Remove(attachment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
