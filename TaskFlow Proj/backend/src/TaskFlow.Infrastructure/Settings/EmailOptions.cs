namespace TaskFlow.Infrastructure.Settings;

/// <summary>The <c>Email</c> configuration section. <see cref="SmtpPassword"/> is a secret: set it with
/// <c>dotnet user-secrets set Email:SmtpPassword ...</c> or the <c>Email__SmtpPassword</c> environment
/// variable, never in appsettings.json. It is never stored in the database, returned or logged.</summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public string? SmtpPassword { get; set; }

    /// <summary>Connect/send timeout for one email.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Attempts per reminder email before it is marked failed.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Base address of the web app; when set, reminder emails link to the task.</summary>
    public string? AppBaseUrl { get; set; }

    // First values for the SMTP fields on a new database. After that the Email Settings screen owns them:
    // a value saved there is never overwritten from configuration.
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpSecurity { get; set; }
    public string? SenderAddress { get; set; }
    public string? SenderName { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SendTime { get; set; }

    public bool PasswordConfigured => !string.IsNullOrEmpty(SmtpPassword);

    public override string ToString() =>
        $"EmailOptions {{ SmtpPassword = {(PasswordConfigured ? "***" : "(not set)")}, TimeoutSeconds = {TimeoutSeconds}, MaxAttempts = {MaxAttempts}, AppBaseUrl = {AppBaseUrl} }}";
}
