using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Interfaces.Auth;

namespace TaskFlow.Api.Repositories.Auth;

public class RefreshTokenRepository : BaseRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveForUsersForUpdateAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default) =>
        await Set.Where(token => userIds.Contains(token.UserId) && token.RevokedAt == null).ToListAsync(cancellationToken);
}
