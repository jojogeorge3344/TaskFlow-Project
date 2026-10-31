using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai.Providers;

/// <summary>What a provider call needs, with the key decrypted. Lives only in memory for the length of one call.</summary>
public record AiProviderRuntime(AiProviderKind Kind, string ModelName, string? BaseUrl, string? ApiKey);
