using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Repositories.Assistant;

public class AssistantConversationRepository : BaseRepository<AssistantConversation>, IAssistantConversationRepository
{
    public AssistantConversationRepository(AppDbContext db)
        : base(db)
    {
    }

    public Task<AssistantConversation?> FindLatestForUpdateAsync(int userId, CancellationToken cancellationToken = default) =>
        Set.Where(conversation => conversation.UserId == userId)
            .OrderByDescending(conversation => conversation.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
