namespace TaskFlow.Application.Mail;

/// <summary>Limits and small pure rules for internal mail, shared by validators, services and tests.</summary>
public static class MailRules
{
    public const int MaxSubjectLength = 200;
    public const int MaxBodyLength = 20_000;
    public const int MaxRecipients = 50;
    public const int MaxAddressLength = 320;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 50;
    public const int MaxContacts = 10;

    public const int MaxAttachments = 5;
    public const long MaxAttachmentBytes = 10L * 1024 * 1024;
    public const long MaxTotalAttachmentBytes = 25L * 1024 * 1024;

    /// <summary>Uploads not yet on any mail or draft, per user; keeps abandoned uploads from filling the disk.</summary>
    public const int MaxPendingUploads = 20;
    public const long MaxPendingUploadBytes = 100L * 1024 * 1024;

    public const string SystemSenderName = "TaskFlow";
    public const string ReplyPrefix = "Re: ";
    public const string ForwardPrefix = "Fwd: ";

    /// <summary>File types that are never accepted as attachments.</summary>
    public static readonly IReadOnlySet<string> BlockedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".ps1", ".sh", ".js", ".vbs", ".scr"
    };

    /// <summary>Trimmed, lower-cased, de-duplicated addresses with blanks dropped, in first-seen order.</summary>
    public static IReadOnlyList<string> NormalizeAddresses(IEnumerable<string>? addresses) =>
        (addresses ?? [])
            .Select(address => address?.Trim().ToLowerInvariant() ?? string.Empty)
            .Where(address => address.Length > 0)
            .Distinct()
            .ToList();

    /// <summary>Adds "Re: " or "Fwd: " unless the subject already starts with it, and keeps the length limit.</summary>
    public static string WithPrefix(string prefix, string subject)
    {
        var result = subject.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase) ? subject : prefix + subject;
        return result.Length <= MaxSubjectLength ? result : result[..MaxSubjectLength];
    }

    /// <summary>A one-line preview of a body.</summary>
    public static string Preview(string body, int length = 120)
    {
        var flat = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= length ? flat : flat[..length] + "…";
    }

    /// <summary>A display-safe file name: the last path segment only, invalid characters removed, at most 255
    /// characters, "file" when nothing is left.</summary>
    public static string SanitizeFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        var invalid = Path.GetInvalidFileNameChars();
        name = new string(name.Where(character => !invalid.Contains(character) && !char.IsControl(character)).ToArray()).Trim().Trim('.');
        if (name.Length == 0)
        {
            return "file";
        }

        return name.Length <= 255 ? name : name[^255..];
    }
}
