using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Assistant.Audits;

public class AssistantAuditRepository : BaseRepository<AssistantAudit>, IAssistantAuditRepository
{
    public AssistantAuditRepository(AppDbContext db)
        : base(db)
    {
    }
}
