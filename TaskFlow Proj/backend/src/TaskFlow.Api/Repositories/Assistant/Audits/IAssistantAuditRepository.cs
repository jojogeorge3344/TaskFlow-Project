using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Assistant.Audits;

/// <summary>The audit trail of TFA actions.</summary>
public interface IAssistantAuditRepository : IBaseRepository<AssistantAudit>
{
}
