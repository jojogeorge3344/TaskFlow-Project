using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Repositories.Assistant;

/// <summary>The audit trail of TFA actions.</summary>
public interface IAssistantAuditRepository : IBaseRepository<AssistantAudit>
{
}
