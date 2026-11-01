using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Models.Entities;

/// <summary>One AI provider: a built-in one (Google Gemini, OpenAI GPT, Ollama) or a custom one an administrator added,
/// reached through the OpenAI-compatible or Ollama API at its own address. Kept apart from <see cref="ApplicationSetting"/>
/// because that table is readable by every signed-in user, while the API key here never leaves the server.</summary>
public class AiProviderConfig
{
    public int Id { get; set; }

    /// <summary>The name shown everywhere, unique ignoring case. Built-in providers use the catalog name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Which client talks to it. For a custom provider, OpenAI-compatible or Ollama.</summary>
    public AiApiType ApiType { get; set; }

    /// <summary>"Local" or "Cloud": whether users' questions leave the organisation (shown in the privacy notice).</summary>
    public string Locality { get; set; } = "Cloud";

    /// <summary>Built-in providers are created by the seeder and never removed.</summary>
    public bool IsBuiltIn { get; set; }

    public List<AiModel> Models { get; set; } = [];
    public bool IsEnabled { get; set; }
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Ollama's address, or a custom provider's (required for those). Built-in Gemini and OpenAI use a fixed address in code.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>The API key protected with ASP.NET Data Protection. Never returned to a client.</summary>
    public string? EncryptedApiKey { get; set; }

    /// <summary>The last four characters of the key, for the administrator's "key set" display.</summary>
    public string? KeyLast4 { get; set; }

    public int? UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
