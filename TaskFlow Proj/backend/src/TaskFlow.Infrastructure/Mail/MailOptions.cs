namespace TaskFlow.Infrastructure.Mail;

/// <summary>The <c>Mail</c> configuration section for internal mail.</summary>
public class MailOptions
{
    public const string SectionName = "Mail";

    /// <summary>Folder for attachment files; a relative path resolves against the storage root (<c>Storage:Root</c>),
    /// like the database path, so the default sits next to taskflow.db. Include it in
    /// backups together with the database.</summary>
    public string AttachmentsPath { get; set; } = "mail-attachments";

    /// <summary>Base address of the web app; reminder mails link to the task under it.</summary>
    public string? AppBaseUrl { get; set; }
}
