using TaskFlow.Api.Dtos.Assistant;

namespace TaskFlow.Api.Services.Assistant.Toolbox;

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
