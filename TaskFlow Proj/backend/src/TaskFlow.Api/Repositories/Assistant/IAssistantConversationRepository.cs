using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Repositories.Assistant;

/// <summary>A user's TFA conversations; the latest one is the active one.</summary>
public interface IAssistantConversationRepository : IBaseRepository<AssistantConversation>
{
    /// <summary>The tracked latest conversation of the user, or null.</summary>
    Task<AssistantConversation?> FindLatestForUpdateAsync(int userId, CancellationToken cancellationToken = default);
}
