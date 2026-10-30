using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Models.Entities;

/// <summary>Organisation-wide configuration of one AI provider. Kept apart from <see cref="ApplicationSetting"/>
/// because that table is readable by every signed-in user, while the API key here never leaves the server.</summary>
public class AiProviderConfig
{
    public int Id { get; set; }
    public AiProviderKind Kind { get; set; }
    public bool IsEnabled { get; set; }
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Only used by Ollama. Cloud providers use a fixed address in code.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>The API key protected with ASP.NET Data Protection. Never returned to a client.</summary>
    public string? EncryptedApiKey { get; set; }

    /// <summary>The last four characters of the key, for the administrator's "key set" display.</summary>
    public string? KeyLast4 { get; set; }

    public int? UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
