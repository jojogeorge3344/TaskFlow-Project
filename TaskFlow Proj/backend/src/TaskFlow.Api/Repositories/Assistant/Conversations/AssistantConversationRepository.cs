using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Assistant.Conversations;

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
