using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Application.Assistant;

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

public static class AssistantRules
{
    public const int MaxMessageLength = 2000;
    public const int HistoryMessages = 12;
    public const int HistoryCharacters = 6000;
    public const int MaxToolRounds = 4;

    /// <summary>How many of the latest messages the panel loads. Older ones stay stored but are not sent to the browser.</summary>
    public const int ConversationMessages = 100;
}
