using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Repositories.Assistant.Audits;

public class AssistantAuditRepository : BaseRepository<AssistantAudit>, IAssistantAuditRepository
{
    public AssistantAuditRepository(AppDbContext db)
        : base(db)
    {
    }
}
