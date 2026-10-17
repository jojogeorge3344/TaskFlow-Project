namespace TaskFlow.Domain.Entities;

/// <summary>A file attached to a mail. Until <see cref="MessageId"/> is set it is an upload only its uploader can
/// see. The content lives in the attachment store under <see cref="StorageKey"/>, never under the user's name.</summary>
public class MailAttachment
{
    public int Id { get; set; }
    public int? MessageId { get; set; }
    public MailMessage? Message { get; set; }
    public int UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>Name of the stored file; forwarded copies share it, which is safe because files never change.</summary>
    public Guid StorageKey { get; set; }

    public DateTime CreatedAt { get; set; }
}
