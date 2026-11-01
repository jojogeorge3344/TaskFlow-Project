using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Dtos.Assistant;

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

/// <summary>A TaskFlow user as the assistant names them: the server, not the model, decides who a name means.</summary>
public record ResolvedUser(int Id, string FullName, string Email, string Role, bool IsActive);
