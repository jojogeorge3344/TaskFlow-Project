using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Assistant;

/// <summary>A provider a user may choose in the chat model selector. Only configured providers are listed.</summary>
public record AiProviderOptionDto(AiProviderKind Kind, string DisplayName, string ModelName, string Locality);

/// <summary>The administrator's view of one provider. The API key is never included; only whether one is set and its
/// last four characters.</summary>
public record AiProviderConfigDto(
    AiProviderKind Kind,
    string DisplayName,
    string Locality,
    bool RequiresKey,
    bool IsEnabled,
    string ModelName,
    string? BaseUrl,
    bool KeySet,
    string? KeyLast4,
    bool IsConfigured,
    bool KeyReadable);

/// <summary>One provider's changes. An empty <see cref="ApiKey"/> keeps the stored key; <see cref="ClearKey"/> removes it.</summary>
public record UpdateAiProviderRequest(
    AiProviderKind? Kind,
    bool? IsEnabled,
    string? ModelName,
    string? BaseUrl,
    string? ApiKey,
    bool? ClearKey);

public record UpdateAiProvidersRequest(IReadOnlyList<UpdateAiProviderRequest>? Providers);

/// <summary>What a provider call needs, with the key decrypted. Lives only in memory for the length of one call.</summary>
public record AiProviderRuntime(AiProviderKind Kind, string ModelName, string? BaseUrl, string? ApiKey);

/// <summary>The outcome of an administrator's "Test connection". <see cref="Message"/> is a fixed message, never the
/// provider's own error text.</summary>
public record AiProviderTestResultDto(bool Ok, string Message, ProviderFailureKind? FailureKind);
