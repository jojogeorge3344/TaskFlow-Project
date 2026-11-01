using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Common.Constants;

/// <summary>Fixed facts about each provider: display name, where it runs, whether it needs a key, and its defaults.</summary>
public static class AiProviderCatalog
{
    public const string Local = "Local";
    public const string Cloud = "Cloud";

    public static IReadOnlyList<AiProviderKind> All { get; } = [AiProviderKind.Ollama, AiProviderKind.Gemini, AiProviderKind.OpenAI];

    public static string DisplayName(AiProviderKind kind) => kind switch
    {
        AiProviderKind.Ollama => "Ollama",
        AiProviderKind.Gemini => "Google Gemini",
        AiProviderKind.OpenAI => "OpenAI GPT",
        _ => kind.ToString()
    };

    public static string Locality(AiProviderKind kind) => kind == AiProviderKind.Ollama ? Local : Cloud;

    public static bool RequiresKey(AiProviderKind kind) => kind != AiProviderKind.Ollama;

    public static string DefaultModel(AiProviderKind kind) => kind switch
    {
        AiProviderKind.Ollama => "llama3.2:3b",
        AiProviderKind.Gemini => "gemini-3.8-flash",
        AiProviderKind.OpenAI => "gpt-4o-mini",
        _ => string.Empty
    };

    public const string DefaultOllamaBaseUrl = "http://localhost:11434";

    private const string GeminiModelPrefix = "models/";

    /// <summary>The model name as it is tested and saved: trimmed, and for Gemini without the <c>models/</c> prefix
    /// Google AI Studio shows (the provider adds it to the URL itself).</summary>
    public static string NormalizeModelName(AiProviderKind kind, string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return kind == AiProviderKind.Gemini && trimmed.StartsWith(GeminiModelPrefix, StringComparison.Ordinal)
            ? trimmed[GeminiModelPrefix.Length..]
            : trimmed;
    }

    /// <summary>Whether a normalised name looks like the provider's model ID. Gemini IDs are lowercase letters, digits,
    /// dots and hyphens (gemini-3.8-flash), so a key's label such as "Gemini_TF_AI" is refused before Google is called.
    /// OpenAI and Ollama IDs may hold uppercase, "_", ":" and "/" (fine-tuned IDs, Ollama tags), so only spaces are refused.</summary>
    public static bool IsValidModelId(AiProviderKind kind, string name) => kind == AiProviderKind.Gemini
        ? name.Length > 0
          && (char.IsAsciiLetterLower(name[0]) || char.IsAsciiDigit(name[0]))
          && name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '-')
        : !name.Any(char.IsWhiteSpace);

    public static string ModelIdMessage(AiProviderKind kind) => kind == AiProviderKind.Gemini
        ? $"A Google Gemini model name is its model ID: lowercase letters, digits, dots and hyphens, for example {DefaultModel(kind)}. The name you gave your key in Google AI Studio is not a model ID."
        : $"A model name is the provider's model ID, without spaces, for example {DefaultModel(kind)}.";

    /// <summary>The models offered in the dropdown from the first run, on every machine. They are inserted into the
    /// database by the seeder; an administrator can add more in Settings. The first one is the default.</summary>
    public static IReadOnlyList<string> BuiltInModels(AiProviderKind kind) => kind switch
    {
        AiProviderKind.Ollama => ["llama3.2:3b", "llama3.2:1b", "llama3.1:8b", "qwen2.5:7b", "mistral:7b"],
        AiProviderKind.Gemini => ["gemini-3.8-flash", "gemini-3.5-flash", "gemini-3.5-flash-lite"],
        AiProviderKind.OpenAI => ["gpt-4o-mini", "gpt-4o", "gpt-4.1-mini"],
        _ => []
    };
}
