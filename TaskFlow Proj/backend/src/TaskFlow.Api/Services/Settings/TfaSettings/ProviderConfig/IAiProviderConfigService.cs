using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Integrations.Ai.Providers;

namespace TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

/// <summary>Organisation-wide AI provider configuration: the built-in providers and those an administrator added. Keys are
/// stored encrypted and are never returned. Providers are identified by id.</summary>
public interface IAiProviderConfigService
{
    /// <summary>The providers that are enabled and fully configured, for the chat model selector.</summary>
    Task<IReadOnlyList<AiProviderOptionDto>> GetAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Every provider with its configuration status, for the administrator: built-in ones first, then custom ones by name.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> GetAdminConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves an already-validated request and returns the resulting status of every provider.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> SaveAsync(int userId, UpdateAiProvidersRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes a model an administrator added. A built-in model, or the one TFA uses, cannot be removed. A custom
    /// provider left with no models is removed too.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> RemoveModelAsync(int providerId, string name, CancellationToken cancellationToken = default);

    /// <summary>What a call to the provider needs, or null when the provider is not configured.</summary>
    Task<AiProviderRuntime?> GetRuntimeAsync(int providerId, CancellationToken cancellationToken = default);

    /// <exception cref="Common.Exceptions.NotFoundException">No provider has this id.</exception>
    Task<AiProviderConfig> GetProviderAsync(int providerId, CancellationToken cancellationToken = default);

    /// <summary>What a test of values that are not saved yet needs. For an existing <paramref name="provider"/>: the typed
    /// key, or else the saved one; for an address-based provider the typed address, then the saved one, then the default.
    /// For a new provider (<paramref name="provider"/> null) everything comes from <see cref="AddAiModelRequest.NewProvider"/>,
    /// with id 0. Nothing is written.</summary>
    /// <exception cref="Common.Exceptions.RequestValidationException">The model ID is invalid for the API type, a built-in
    /// cloud provider has no typed or readable saved key, or a key was sent for Ollama.</exception>
    Task<AiProviderRuntime> ResolveCandidateRuntimeAsync(AiProviderConfig? provider, AddAiModelRequest request, CancellationToken cancellationToken = default);

    /// <summary>Checks a provider typed on the Add or Edit screen that does not exist yet: name 2–40 characters and not
    /// taken (ignoring case), API type OpenAI-compatible or Ollama, a safe http or https address, Local or Cloud.</summary>
    System.Threading.Tasks.Task ValidateNewProviderAsync(NewAiProviderRequest newProvider, CancellationToken cancellationToken = default);

    /// <summary>Makes a tested model the one TFA uses, in one save: creates its provider when the runtime's id is 0, adds the
    /// model to the list if it is new, stores the key when <paramref name="saveKey"/> is true (or the address), enables its
    /// provider and switches the others off.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> ActivateModelAsync(int userId, AiProviderRuntime runtime, bool saveKey, CancellationToken cancellationToken = default);

    /// <summary>Makes a listed model the one TFA uses ("Use this model"): enables its provider and switches the others off.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">The provider or the model does not exist.</exception>
    /// <exception cref="Common.Exceptions.RequestValidationException">A built-in cloud provider has no readable saved key.</exception>
    Task<IReadOnlyList<AiProviderConfigDto>> UseModelAsync(int userId, int providerId, string modelName, CancellationToken cancellationToken = default);

    /// <summary>The saved key or address with a listed model, whether or not the provider is switched on, for testing that
    /// model from the list. Null when a built-in cloud provider has no readable key.</summary>
    Task<AiProviderRuntime?> GetSavedRuntimeForModelAsync(int providerId, string modelName, CancellationToken cancellationToken = default);

    /// <summary>Refuses an unknown model (404), a built-in one, or the one TFA uses (400 naming <paramref name="action"/>,
    /// "edit" or "delete").</summary>
    System.Threading.Tasks.Task EnsureEditableAsync(int providerId, string name, string action, CancellationToken cancellationToken = default);

    /// <summary>Saves a tested edit in one save. On the same provider: renames the model (and a switched-off provider's saved
    /// name with it). On another provider (<paramref name="target"/> with a different id, or id 0 for a new one): moves the
    /// model there, creating the target when it is new and removing the source when it is a custom provider left empty. Stores
    /// the key when <paramref name="saveKey"/> is true, or the address. Which model TFA uses is unchanged.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> UpdateModelAsync(int userId, int sourceProviderId, string currentName, AiProviderRuntime target, bool saveKey, CancellationToken cancellationToken = default);

    /// <summary>Removes a provider's saved key. Every model of that provider is then without a key.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> ClearKeyAsync(int userId, int providerId, CancellationToken cancellationToken = default);

    Task<bool> IsKnownModelAsync(int providerId, string name, CancellationToken cancellationToken = default);
}
