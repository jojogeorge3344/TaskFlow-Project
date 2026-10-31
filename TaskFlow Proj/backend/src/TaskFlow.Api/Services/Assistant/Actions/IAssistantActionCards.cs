using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Services.Assistant.Actions;

/// <summary>Builds the panel's action cards from stored pending actions.</summary>
public interface IAssistantActionCards
{
    Task<IReadOnlyDictionary<Guid, AssistantPendingActionDto>> GetCardsAsync(
        int userId,
        IReadOnlyCollection<Guid> actionIds,
        CancellationToken cancellationToken = default);
}
