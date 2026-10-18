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
        AiProviderKind.Ollama => "llama3.1:8b",
        AiProviderKind.Gemini => "gemini-2.0-flash",
        AiProviderKind.OpenAI => "gpt-4o-mini",
        _ => string.Empty
    };

    public const string DefaultOllamaBaseUrl = "http://localhost:11434";
}
