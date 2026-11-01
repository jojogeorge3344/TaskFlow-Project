using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Services.Assistant.Access;

namespace TaskFlow.Api.Services.Assistant.Chat;

/// <summary>TFA's conversation. Everything works on the caller's own conversation: a conversation that is not the
/// caller's is reported as not found, never as forbidden.</summary>
public interface IAssistantService
{
    /// <summary>The caller's current conversation, or an empty one when they have not chatted yet.</summary>
    Task<AssistantConversationDto> GetConversationAsync(AssistantCaller caller, CancellationToken cancellationToken = default);

    /// <summary>Answers one message with the chosen provider. A blocked or rate-limited message returns a fixed notice.
    /// A provider failure returns <see cref="AssistantReplyDto.Failure"/> and stores nothing, so the user can retry.</summary>
    Task<AssistantReplyDto> SendAsync(AssistantCaller caller, SendAssistantMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Starts a new conversation; earlier messages are no longer used as context.</summary>
    Task NewConversationAsync(AssistantCaller caller, CancellationToken cancellationToken = default);
}
