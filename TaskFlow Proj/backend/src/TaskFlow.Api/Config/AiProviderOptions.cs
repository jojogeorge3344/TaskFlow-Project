namespace TaskFlow.Api.Config;

/// <summary>The <c>AiProviders</c> configuration section.</summary>
public class AiProviderOptions
{
    public const string SectionName = "AiProviders";

    /// <summary>Folder for the Data Protection key ring that encrypts API keys. A relative path resolves against the
    /// storage root (<c>Storage:Root</c>), like the database path. Keep it out of backups that also hold the database.</summary>
    public string KeyRingPath { get; set; } = "dp-keys";

    /// <summary>How long one provider call may take before it is reported as a timeout.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
