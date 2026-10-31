namespace TaskFlow.Api.Services.Communication.Mail.Attachments;

/// <summary>An attachment ready to stream to the caller.</summary>
public sealed record MailAttachmentDownload(Stream Content, string FileName, string ContentType);
