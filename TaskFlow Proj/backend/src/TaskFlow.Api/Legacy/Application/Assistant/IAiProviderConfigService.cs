using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Application.Assistant;

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
}
