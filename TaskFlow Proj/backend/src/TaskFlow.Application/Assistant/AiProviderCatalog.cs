using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Assistant;

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
        AiProviderKind.Gemini => "gemini-2.0-flash",
        AiProviderKind.OpenAI => "gpt-4o-mini",
        _ => string.Empty
    };

    public const string DefaultOllamaBaseUrl = "http://localhost:11434";

    /// <summary>The models offered in the dropdown from the first run, on every machine. They are inserted into the
    /// database by the seeder; an administrator can add more in Settings. The first one is the default.</summary>
    public static IReadOnlyList<string> BuiltInModels(AiProviderKind kind) => kind switch
    {
        AiProviderKind.Ollama => ["llama3.2:3b", "llama3.2:1b", "llama3.1:8b", "qwen2.5:7b", "mistral:7b"],
        AiProviderKind.Gemini => ["gemini-2.0-flash", "gemini-2.5-flash", "gemini-2.5-pro"],
        AiProviderKind.OpenAI => ["gpt-4o-mini", "gpt-4o", "gpt-4.1-mini"],
        _ => []
    };
}
