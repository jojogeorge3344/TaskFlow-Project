using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Assistant.Messages;

/// <summary>The stored messages of TFA conversations.</summary>
public interface IAssistantMessageRepository : IBaseRepository<AssistantMessage>
{
    /// <summary>The newest <paramref name="take"/> messages of the conversation, newest first.</summary>
    Task<IReadOnlyList<AssistantMessage>> GetNewestAsync(int conversationId, int take, CancellationToken cancellationToken = default);
}
