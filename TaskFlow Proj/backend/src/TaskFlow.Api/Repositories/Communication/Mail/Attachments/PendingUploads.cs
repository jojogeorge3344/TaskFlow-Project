namespace TaskFlow.Api.Repositories.Communication.Mail.Attachments;

/// <summary>How many uploads a user has waiting to be attached, and their total size.</summary>
public record PendingUploads(int Count, long Bytes);
