using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Dtos.Settings.TfaSettings;

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
    bool KeyReadable,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> CustomModels);

/// <summary>One provider's changes. An empty <see cref="ApiKey"/> keeps the stored key; <see cref="ClearKey"/> removes it.</summary>
public record UpdateAiProviderRequest(
    AiProviderKind? Kind,
    bool? IsEnabled,
    string? ModelName,
    string? BaseUrl,
    string? ApiKey,
    bool? ClearKey);

public record UpdateAiProvidersRequest(IReadOnlyList<UpdateAiProviderRequest>? Providers);

/// <summary>The outcome of an administrator's "Test connection". <see cref="Message"/> is a fixed message, never the
/// provider's own error text.</summary>
public record AiProviderTestResultDto(bool Ok, string Message, ProviderFailureKind? FailureKind);

/// <summary>A model to add from the Add AI model page. It is tested with these values first and saved only if the test
/// passes. An empty <see cref="ApiKey"/> uses the provider's saved key; <see cref="BaseUrl"/> is only for Ollama. Also the body
/// of an edit, where <see cref="ClearKey"/> removes the provider's key without a test (and without other changes).</summary>
public record AddAiModelRequest(string? ModelName, string? ApiKey, string? BaseUrl, bool? ClearKey = null);

/// <summary>The listed model to switch TFA to ("Use this model"), or to test from the list.</summary>
public record AiModelChoiceRequest(string? ModelName);

/// <summary>The outcome of adding a model. When <see cref="Ok"/> is false nothing was saved and <see cref="Providers"/> is
/// null; when it is true the model is the one TFA uses and <see cref="Providers"/> is every provider's new status.</summary>
public record AddAiModelResultDto(bool Ok, string Message, ProviderFailureKind? FailureKind, IReadOnlyList<AiProviderConfigDto>? Providers);
