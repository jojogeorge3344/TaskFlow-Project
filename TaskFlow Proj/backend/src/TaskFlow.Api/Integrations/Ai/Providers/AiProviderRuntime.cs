using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Integrations.Ai.Providers;

/// <summary>What a provider call needs, with the key decrypted. Lives only in memory for the length of one call.
/// <see cref="ApiType"/> picks the client; <see cref="ProviderId"/> and <see cref="Name"/> say which provider it is (a
/// built-in one or a custom one). <see cref="BaseUrl"/> is null for built-in Gemini and OpenAI, which use their fixed
/// address.</summary>
public record AiProviderRuntime(
    int ProviderId,
    AiApiType ApiType,
    string Name,
    bool IsLocal,
    string ModelName,
    string? BaseUrl,
    string? ApiKey);
