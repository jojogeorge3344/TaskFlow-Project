namespace TaskFlow.Api.Config;

/// <summary>The <c>AiProviders</c> configuration section.</summary>
public class AiProviderOptions
{
    public const string SectionName = "AiProviders";

    /// <summary>Folder for the Data Protection key ring that encrypts API keys. A relative path resolves against the
    /// storage root (<c>Storage:Root</c>), like the database path. Keep it out of backups that also hold the database.</summary>
    public string KeyRingPath { get; set; } = "dp-keys";

    /// <summary>How long one call to a cloud provider (Gemini, OpenAI), including a Gemini embedding call, may take before it is reported as a timeout.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>How long one call to Ollama chat may take. Longer than the cloud timeout because a local model on a
    /// laptop CPU can take over a minute to read a prompt that carries reference documents.</summary>
    public int OllamaTimeoutSeconds { get; set; } = 180;

    /// <summary>The context window sent to Ollama (<c>options.num_ctx</c>). Ollama's own default is smaller and it truncates
    /// a longer prompt silently, which would cut off the reference documents.</summary>
    public int OllamaContextTokens { get; set; } = 8192;

    /// <summary>A Google Gemini API key for the first run, set with <c>dotnet user-secrets set "AiProviders:GeminiApiKey" "&lt;key&gt;"</c>
    /// or the <c>AiProviders__GeminiApiKey</c> environment variable, never in appsettings.json. The seeder stores it encrypted
    /// when the Gemini provider has no key; after that the key is managed in TFA Settings.</summary>
    public string? GeminiApiKey { get; set; }
}
