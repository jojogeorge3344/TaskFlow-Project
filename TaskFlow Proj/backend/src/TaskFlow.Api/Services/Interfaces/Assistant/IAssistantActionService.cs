using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Services.Interfaces.Assistant;

/// <summary>Records what happened to each action, without message bodies, passwords or keys.</summary>
public interface IAssistantAuditLog
{
    Task RecordAsync(int userId, AssistantActionKind kind, string outcome, CancellationToken cancellationToken = default);
}

/// <summary>Pending actions. The model can only create them (through tools); only the user's own confirm request runs one.</summary>
public interface IAssistantActionService : IAssistantActionCards
{
    Task<AssistantPendingActionDto> CreateAsync(
        AssistantCaller caller,
        int conversationId,
        AssistantActionKind kind,
        object payload,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces a pending mail's recipients, subject and body with the user's edits. Null when the action is
    /// not the caller's.</summary>
    Task<AssistantPendingActionDto?> EditMailAsync(AssistantCaller caller, Guid id, EditMailActionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Runs the action once, after checking it again against the caller's current permissions. Null when the
    /// action is not the caller's.</summary>
    /// <exception cref="TaskFlow.Api.Common.Exceptions.AssistantActionConflictException">The action is expired, cancelled or already done.</exception>
    Task<AssistantActionResultDto?> ConfirmAsync(AssistantCaller caller, Guid id, CancellationToken cancellationToken = default);

    Task<AssistantPendingActionDto?> CancelAsync(AssistantCaller caller, Guid id, CancellationToken cancellationToken = default);
}
