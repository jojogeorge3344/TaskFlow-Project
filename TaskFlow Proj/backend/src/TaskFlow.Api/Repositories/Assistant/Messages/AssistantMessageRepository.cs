using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Repositories.Assistant.Messages;

public class AssistantMessageRepository : BaseRepository<AssistantMessage>, IAssistantMessageRepository
{
    public AssistantMessageRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<AssistantMessage>> GetNewestAsync(int conversationId, int take, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderByDescending(message => message.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
}
