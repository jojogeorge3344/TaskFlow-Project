using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Application.Assistant;

// ---- What is stored with a pending action: exactly what the user is shown and what will be executed.

public record MailRecipientRef(int UserId, string FullName, string Email);

public record SendMailPayload(IReadOnlyList<MailRecipientRef> To, string Subject, string Body, int? TaskId);

public record UpdateProfilePayload(string? FullName, string? Email, string CurrentFullName, string CurrentEmail);

public record ChangeRolePayload(int TargetUserId, string TargetName, string CurrentRole, int NewRoleId, string NewRole);

// ---- Requests and results

/// <summary>The user's edits to a prepared mail. Recipients are email addresses of active TaskFlow users.</summary>
public record EditMailActionRequest(IReadOnlyList<string>? To, string? Subject, string? Body);

public record AssistantActionResultDto(
    Guid Id,
    AssistantActionStatus Status,
    string Message,
    AssistantMessageDto? Reply);

/// <summary>The action is not in a state that allows this (expired, already done, cancelled). HTTP 409.</summary>
public class AssistantActionConflictException : Exception
{
    public AssistantActionConflictException(string message)
        : base(message)
    {
    }
}

public static class AssistantActionRules
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>The policy the action needs when it is confirmed, with the words for the fixed refusal.</summary>
    public static (string? Policy, string Description, string WhoCanDoIt) Requirement(AssistantActionKind kind) => kind switch
    {
        AssistantActionKind.ChangeUserRole => (AssistantPolicies.RolesManage, "Changing a user's role", "an authorized administrator"),
        AssistantActionKind.UpdateOwnProfile => (null, "Updating a profile", "the account owner"),
        _ => (null, "Sending mail", "a signed-in user")
    };
}

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
    /// <exception cref="AssistantActionConflictException">The action is expired, cancelled or already done.</exception>
    Task<AssistantActionResultDto?> ConfirmAsync(AssistantCaller caller, Guid id, CancellationToken cancellationToken = default);

    Task<AssistantPendingActionDto?> CancelAsync(AssistantCaller caller, Guid id, CancellationToken cancellationToken = default);
}
