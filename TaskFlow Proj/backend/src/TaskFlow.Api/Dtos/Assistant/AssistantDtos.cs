using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Models.Enums;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Dtos.Assistant;

/// <summary>How a message should look in the panel. Refusal and Notice messages are fixed text, never model output.</summary>
public enum AssistantMessageKind
{
    Text,
    Refusal,
    Notice
}

public record AssistantLinkDto(string Label, string Route);

/// <summary>A pending action as shown in the panel, built from the stored payload and never from model text.</summary>
public record AssistantActionFieldDto(string Label, string Value);

public record AssistantPendingActionDto(
    Guid Id,
    AssistantActionKind Kind,
    AssistantActionStatus Status,
    DateTime ExpiresAt,
    string Title,
    IReadOnlyList<AssistantActionFieldDto> Fields,
    string? Body,
    bool CanEdit);

public record AssistantMessageDto(
    int Id,
    string Role,
    string Content,
    AssistantMessageKind Kind,
    DateTime CreatedAt,
    AiProviderKind? Provider,
    IReadOnlyList<AssistantLinkDto> Links,
    IReadOnlyList<PrioritisedTask> RankedTasks,
    AssistantPendingActionDto? Action);

public record AssistantConversationDto(int? ConversationId, IReadOnlyList<AssistantMessageDto> Messages);

/// <summary>The result of sending a message: the messages to append, or a classified provider failure.</summary>
public record AssistantReplyDto(IReadOnlyList<AssistantMessageDto> Messages, AssistantFailureDto? Failure);

public record SendAssistantMessageRequest(string? Text, AiProviderKind? Provider);

/// <summary>What is stored in <c>AssistantMessage.MetaJson</c> so the panel can redraw a message after a reload.</summary>
public record AssistantMessageMeta(
    AssistantMessageKind Kind,
    IReadOnlyList<AssistantLinkDto>? Links,
    IReadOnlyList<PrioritisedTask>? RankedTasks,
    Guid? ActionId);

/// <summary>One open task in the order TFA recommends.</summary>
public record PrioritisedTask(
    int Rank,
    int TaskId,
    string TaskCode,
    string Title,
    TaskPriority Priority,
    TaskStatus Status,
    DateTime Deadline,
    DateOnly DueDate,
    int DaysUntilDue,
    TaskStage Stage,
    bool IsBlocked);
