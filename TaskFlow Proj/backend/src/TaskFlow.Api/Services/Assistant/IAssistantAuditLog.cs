using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Services.Assistant;

/// <summary>Records what happened to each action, without message bodies, passwords or keys.</summary>
public interface IAssistantAuditLog
{
    Task RecordAsync(int userId, AssistantActionKind kind, string outcome, CancellationToken cancellationToken = default);
}
