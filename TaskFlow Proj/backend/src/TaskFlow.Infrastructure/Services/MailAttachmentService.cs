using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Mail;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Services;

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

    private readonly AppDbContext _dbContext;
    private readonly IMailFileStore _store;
    private readonly TimeProvider _timeProvider;

    public MailAttachmentService(AppDbContext dbContext, IMailFileStore store, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
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

        var (key, size) = await _store.SaveAsync(content, MailRules.MaxAttachmentBytes, cancellationToken);
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
        _dbContext.MailAttachments.Add(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MailAttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes);
    }

    public async Task<MailAttachmentDownload?> OpenAsync(int callerId, int attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _dbContext.MailAttachments
            .AsNoTracking()
            .Where(file => file.Id == attachmentId
                && (file.UploadedByUserId == callerId
                    || (file.MessageId != null && _dbContext.MailboxItems.Any(item =>
                        item.MessageId == file.MessageId && item.OwnerUserId == callerId && item.PurgedAt == null))))
            .Select(file => new { file.FileName, file.ContentType, file.StorageKey })
            .FirstOrDefaultAsync(cancellationToken);
        if (attachment is null)
        {
            return null;
        }

        var stream = _store.OpenRead(attachment.StorageKey);
        return stream is null ? null : new MailAttachmentDownload(stream, attachment.FileName, attachment.ContentType);
    }

    public async Task<bool> DeleteAsync(int callerId, int attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _dbContext.MailAttachments
            .FirstOrDefaultAsync(file => file.Id == attachmentId && file.UploadedByUserId == callerId && file.MessageId == null, cancellationToken);
        if (attachment is null)
        {
            return false;
        }

        // The stored file may be shared by a forwarded copy, so only the record is removed.
        _dbContext.MailAttachments.Remove(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
