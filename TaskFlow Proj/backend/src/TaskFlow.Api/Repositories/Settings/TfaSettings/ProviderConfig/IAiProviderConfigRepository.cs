using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;

/// <summary>The saved settings of each AI provider, at most one row per provider.</summary>
public interface IAiProviderConfigRepository : IBaseRepository<AiProviderConfig>
{
    Task<IReadOnlyList<AiProviderConfig>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Every row, tracked, for saving changes to them.</summary>
    Task<List<AiProviderConfig>> GetAllForUpdateAsync(CancellationToken cancellationToken = default);

    Task<AiProviderConfig?> GetAsync(AiProviderKind kind, CancellationToken cancellationToken = default);

    /// <summary>Whether a provider's saved model is this one.</summary>
    Task<bool> UsesModelAsync(AiProviderKind kind, string modelName, CancellationToken cancellationToken = default);
}
