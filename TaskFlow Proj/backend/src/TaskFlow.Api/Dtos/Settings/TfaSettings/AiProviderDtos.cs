using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Dtos.Settings.TfaSettings;

/// <summary>A provider a user may choose in the chat model selector. Only configured providers are listed.</summary>
public record AiProviderOptionDto(int ProviderId, string DisplayName, string ModelName, string Locality);

/// <summary>The administrator's view of one provider, built-in or custom. The API key is never included; only whether one
/// is set and its last four characters.</summary>
public record AiProviderConfigDto(
    int ProviderId,
    string DisplayName,
    AiApiType ApiType,
    bool IsBuiltIn,
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
    int? ProviderId,
    bool? IsEnabled,
    string? ModelName,
    string? BaseUrl,
    string? ApiKey,
    bool? ClearKey);

public record UpdateAiProvidersRequest(IReadOnlyList<UpdateAiProviderRequest>? Providers);

/// <summary>The outcome of an administrator's "Test connection". <see cref="Message"/> is a fixed message, never the
/// provider's own error text.</summary>
public record AiProviderTestResultDto(bool Ok, string Message, ProviderFailureKind? FailureKind);

/// <summary>A provider typed on the Add or Edit AI model screen that does not exist yet. It is created only when the
/// connection test passes. <see cref="ApiType"/> is OpenAI (OpenAI-compatible) or Ollama; <see cref="Locality"/> is
/// "Local" or "Cloud" (default Cloud).</summary>
public record NewAiProviderRequest(string? Name, AiApiType? ApiType, string? BaseUrl, string? Locality);

/// <summary>A model to add from the Add AI model page, or the body of an edit. It is tested with these values first and
/// saved only if the test passes. The provider is <see cref="ProviderId"/> (an existing one) or
/// <see cref="NewProvider"/> (created on a pass); on an edit, giving neither keeps the model's provider. An empty
/// <see cref="ApiKey"/> uses the provider's saved key; <see cref="BaseUrl"/> is an existing Ollama provider's address.
/// <see cref="ClearKey"/> (edit only) removes the provider's key without a test, and without other changes.</summary>
public record AddAiModelRequest(
    string? ModelName,
    string? ApiKey,
    string? BaseUrl,
    bool? ClearKey = null,
    int? ProviderId = null,
    NewAiProviderRequest? NewProvider = null);

/// <summary>The listed model to switch TFA to ("Use this model"), or to test from the list.</summary>
public record AiModelChoiceRequest(string? ModelName);

/// <summary>The outcome of adding a model. When <see cref="Ok"/> is false nothing was saved and <see cref="Providers"/> is
/// null; when it is true the model is the one TFA uses and <see cref="Providers"/> is every provider's new status.</summary>
public record AddAiModelResultDto(bool Ok, string Message, ProviderFailureKind? FailureKind, IReadOnlyList<AiProviderConfigDto>? Providers);
