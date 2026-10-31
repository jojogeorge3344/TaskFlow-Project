using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Integrations.Ai;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Services.Assistant;

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
