using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Repositories.Assistant;

public class AssistantPendingActionRepository : BaseRepository<AssistantPendingAction>, IAssistantPendingActionRepository
{
    public AssistantPendingActionRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<AssistantPendingAction>> GetForUserAsync(
        int userId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(action => action.UserId == userId && ids.Contains(action.Id))
            .ToListAsync(cancellationToken);

    public Task<AssistantPendingAction?> FindForUserAsync(Guid id, int userId, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId, cancellationToken);

    public Task<AssistantPendingAction?> FindForUserForUpdateAsync(Guid id, int userId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId, cancellationToken);

    public async Task<bool> TryClaimAsync(Guid id, DateTime now, CancellationToken cancellationToken = default) =>
        await Set
            .Where(candidate => candidate.Id == id && candidate.Status == AssistantActionStatus.Pending)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(candidate => candidate.Status, AssistantActionStatus.Confirmed)
                    .SetProperty(candidate => candidate.ResolvedAt, now),
                cancellationToken) > 0;

    public System.Threading.Tasks.Task SetStatusAsync(Guid id, AssistantActionStatus status, DateTime now, CancellationToken cancellationToken = default) =>
        Set.Where(candidate => candidate.Id == id)
            .ExecuteUpdateAsync(
                set => set.SetProperty(candidate => candidate.Status, status).SetProperty(candidate => candidate.ResolvedAt, now),
                cancellationToken);
}
