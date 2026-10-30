using TaskFlow.Api.Integrations.Interfaces.Ai;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Application.Assistant;

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

/// <summary>The tools the model may call, filtered to what the caller's role may use.</summary>
public interface IAssistantToolbox
{
    /// <summary>The tool definitions to advertise to the model for this caller.</summary>
    Task<IReadOnlyList<ChatToolDefinition>> DefinitionsForAsync(AssistantCaller caller, CancellationToken cancellationToken = default);

    Task<ToolOutcome> ExecuteAsync(AssistantCaller caller, int conversationId, ChatToolCall call, CancellationToken cancellationToken = default);
}

/// <summary>What a tool produced. <see cref="ResultForModel"/> is data for the model to read. A non-null
/// <see cref="FixedReply"/> ends the turn with that exact text (a refusal, or a prepared action) instead of asking the
/// model to word it.</summary>
public record ToolOutcome(
    string ResultForModel,
    string? FixedReply = null,
    AssistantMessageKind FixedKind = AssistantMessageKind.Text,
    IReadOnlyList<AssistantLinkDto>? Links = null,
    IReadOnlyList<PrioritisedTask>? RankedTasks = null,
    Guid? PendingActionId = null);

/// <summary>Builds the panel's action cards from stored pending actions.</summary>
public interface IAssistantActionCards
{
    Task<IReadOnlyDictionary<Guid, AssistantPendingActionDto>> GetCardsAsync(
        int userId,
        IReadOnlyCollection<Guid> actionIds,
        CancellationToken cancellationToken = default);
}
