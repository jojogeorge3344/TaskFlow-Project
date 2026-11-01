using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Common.Constants;

/// <summary>Fixed facts about each provider: display name, where it runs, whether it needs a key, and its defaults.</summary>
public static class AiProviderCatalog
{
    public const string Local = "Local";
    public const string Cloud = "Cloud";

    /// <summary>The built-in providers in the order they are listed. Google Gemini is first because it is the default.</summary>
    public static IReadOnlyList<AiApiType> All { get; } = [AiApiType.Gemini, AiApiType.OpenAI, AiApiType.Ollama];

    public static string DisplayName(AiApiType kind) => kind switch
    {
        AiApiType.Ollama => "Ollama",
        AiApiType.Gemini => "Google Gemini",
        AiApiType.OpenAI => "OpenAI GPT",
        _ => kind.ToString()
    };

    public static string Locality(AiApiType kind) => kind == AiApiType.Ollama ? Local : Cloud;

    public static bool RequiresKey(AiApiType kind) => kind != AiApiType.Ollama;

    public static string DefaultModel(AiApiType kind) => kind switch
    {
        AiApiType.Ollama => "llama3.2:3b",
        AiApiType.Gemini => "gemini-3.8-flash",
        AiApiType.OpenAI => "gpt-4o-mini",
        _ => string.Empty
    };

    public const string DefaultOllamaBaseUrl = "http://localhost:11434";

    private const string GeminiModelPrefix = "models/";

    /// <summary>The model name as it is tested and saved: trimmed, and for Gemini without the <c>models/</c> prefix
    /// Google AI Studio shows (the provider adds it to the URL itself).</summary>
    public static string NormalizeModelName(AiApiType kind, string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return kind == AiApiType.Gemini && trimmed.StartsWith(GeminiModelPrefix, StringComparison.Ordinal)
            ? trimmed[GeminiModelPrefix.Length..]
            : trimmed;
    }

    /// <summary>Whether a normalised name looks like the provider's model ID. Gemini IDs are lowercase letters, digits,
    /// dots and hyphens (gemini-3.8-flash), so a key's label such as "Gemini_TF_AI" is refused before Google is called.
    /// OpenAI-compatible and Ollama IDs may hold uppercase, "_", ":" and "/" (fine-tuned IDs, Ollama tags), so only spaces and
    /// "@" are refused; no model ID has an "@", so an email address the browser filled in is caught before anything is sent.</summary>
    public static bool IsValidModelId(AiApiType kind, string name) => kind == AiApiType.Gemini
        ? name.Length > 0
          && (char.IsAsciiLetterLower(name[0]) || char.IsAsciiDigit(name[0]))
          && name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '-')
        : !name.Any(char.IsWhiteSpace) && !name.Contains('@');

    public static string ModelIdMessage(AiApiType kind) => kind == AiApiType.Gemini
        ? $"A Google Gemini model name is its model ID: lowercase letters, digits, dots and hyphens, for example {DefaultModel(kind)}. The name you gave your key in Google AI Studio is not a model ID."
        : $"A model name is the provider's model ID, without spaces or @, for example {DefaultModel(kind)}.";

    /// <summary>The models offered in the dropdown from the first run, on every machine. They are inserted into the
    /// database by the seeder; an administrator can add more in Settings. The first one is the default.</summary>
    public static IReadOnlyList<string> BuiltInModels(AiApiType kind) => kind switch
    {
        AiApiType.Ollama => ["llama3.2:3b", "llama3.2:1b", "llama3.1:8b", "qwen2.5:7b", "mistral:7b"],
        AiApiType.Gemini => ["gemini-3.8-flash", "gemini-3.5-flash", "gemini-3.5-flash-lite"],
        AiApiType.OpenAI => ["gpt-4o-mini", "gpt-4o", "gpt-4.1-mini"],
        _ => []
    };
}
