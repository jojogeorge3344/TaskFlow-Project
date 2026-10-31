using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Auth.RefreshTokens;

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

    public Task<RefreshToken?> FindByHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public Task<RefreshToken?> FindByHashWithUserForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Set.Include(token => token.User).ThenInclude(user => user!.Role)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
}
