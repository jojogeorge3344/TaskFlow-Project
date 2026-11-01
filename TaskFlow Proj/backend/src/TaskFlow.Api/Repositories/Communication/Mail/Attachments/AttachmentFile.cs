namespace TaskFlow.Api.Repositories.Communication.Mail.Attachments;

/// <summary>What a download needs from an attachment record.</summary>
public record AttachmentFile(string FileName, string ContentType, Guid StorageKey);
