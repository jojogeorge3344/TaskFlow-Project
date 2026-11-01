using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.Providers;

namespace TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

/// <summary>Organisation-wide AI provider configuration. Keys are stored encrypted and are never returned.</summary>
public interface IAiProviderConfigService
{
    /// <summary>The providers that are enabled and fully configured, for the chat model selector.</summary>
    Task<IReadOnlyList<AiProviderOptionDto>> GetAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Every provider with its configuration status, for the administrator.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> GetAdminConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves an already-validated request and returns the resulting status of every provider.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> SaveAsync(int userId, UpdateAiProvidersRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes a model an administrator added. A built-in model, or the one saved for its server, cannot be removed.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> RemoveModelAsync(AiProviderKind kind, string name, CancellationToken cancellationToken = default);

    /// <summary>What a call to the provider needs, or null when the provider is not configured.</summary>
    Task<AiProviderRuntime?> GetRuntimeAsync(AiProviderKind kind, CancellationToken cancellationToken = default);

    /// <summary>What a test of a model that is not saved yet needs: the typed key, or else the saved one; for Ollama the
    /// typed address, then the saved one, then the default. Nothing is written.</summary>
    /// <exception cref="Common.Exceptions.RequestValidationException">A cloud provider has no typed or readable saved key, or a key was sent for Ollama.</exception>
    Task<AiProviderRuntime> ResolveCandidateRuntimeAsync(AiProviderKind kind, AddAiModelRequest request, CancellationToken cancellationToken = default);

    /// <summary>Makes a tested model the one TFA uses, in one save: adds it to the list if it is new, stores the key when
    /// <paramref name="saveKey"/> is true (or Ollama's address), enables its provider and switches the others off.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> ActivateModelAsync(int userId, AiProviderRuntime runtime, bool saveKey, CancellationToken cancellationToken = default);

    /// <summary>Makes a listed model the one TFA uses ("Use this model"): enables its provider and switches the others off.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">The model is not in the provider's list.</exception>
    /// <exception cref="Common.Exceptions.RequestValidationException">A cloud provider has no readable saved key.</exception>
    Task<IReadOnlyList<AiProviderConfigDto>> UseModelAsync(int userId, AiProviderKind kind, string modelName, CancellationToken cancellationToken = default);

    /// <summary>The saved key or address with a listed model, whether or not the provider is switched on, for testing that
    /// model from the list. Null when a cloud provider has no readable key.</summary>
    Task<AiProviderRuntime?> GetSavedRuntimeForModelAsync(AiProviderKind kind, string modelName, CancellationToken cancellationToken = default);

    /// <summary>Refuses an unknown model (404), a built-in one, or the one TFA uses (400 naming <paramref name="action"/>,
    /// "edit" or "delete").</summary>
    System.Threading.Tasks.Task EnsureEditableAsync(AiProviderKind kind, string name, string action, CancellationToken cancellationToken = default);

    /// <summary>Saves a tested edit in one save: renames the model (and a switched-off provider's saved name with it) and
    /// stores the key when <paramref name="saveKey"/> is true, or Ollama's address. Which model TFA uses is unchanged.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> UpdateModelAsync(int userId, AiProviderKind kind, string currentName, AiProviderRuntime runtime, bool saveKey, CancellationToken cancellationToken = default);

    /// <summary>Removes a cloud provider's saved key. Every model of that provider is then without a key.</summary>
    Task<IReadOnlyList<AiProviderConfigDto>> ClearKeyAsync(int userId, AiProviderKind kind, CancellationToken cancellationToken = default);

    Task<bool> IsKnownModelAsync(AiProviderKind kind, string name, CancellationToken cancellationToken = default);
}
