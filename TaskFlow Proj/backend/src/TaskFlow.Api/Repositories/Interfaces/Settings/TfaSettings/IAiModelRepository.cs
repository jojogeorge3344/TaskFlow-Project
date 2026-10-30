using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Repositories.Interfaces.Settings.TfaSettings;

/// <summary>The model names offered for each provider: the built-in ones and those administrators added.</summary>
public interface IAiModelRepository : IBaseRepository<AiModel>
{
    /// <summary>Every model, built-in ones first, then in the order they were added.</summary>
    Task<IReadOnlyList<AiModel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the model is stored or already staged to be added in this request.</summary>
    Task<bool> IsKnownAsync(AiProviderKind kind, string name, CancellationToken cancellationToken = default);

    /// <summary>The tracked model with this name, for removing it.</summary>
    Task<AiModel?> GetForUpdateAsync(AiProviderKind kind, string name, CancellationToken cancellationToken = default);

    /// <summary>After a save failed on the unique index: un-stages each model this request was adding that another request
    /// has stored in the meantime. Returns whether any was dropped, so the caller can save again.</summary>
    Task<bool> DropAddedModelsStoredElsewhereAsync(CancellationToken cancellationToken = default);
}
