using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Auth.PasswordRecovery;

public class PasswordResetGrantRepository : BaseRepository<PasswordResetGrant>, IPasswordResetGrantRepository
{
    public PasswordResetGrantRepository(AppDbContext db)
        : base(db)
    {
    }

    public Task<PasswordResetGrant?> FindUnusedByHashWithUserForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Set.Include(row => row.User)
            .FirstOrDefaultAsync(row => row.TokenHash == tokenHash && row.UsedAt == null, cancellationToken);

    public async Task<IReadOnlyList<PasswordResetGrant>> GetOtherUnusedForUpdateAsync(
        int userId,
        int exceptGrantId,
        CancellationToken cancellationToken = default) =>
        await Set.Where(row => row.UserId == userId && row.Id != exceptGrantId && row.UsedAt == null).ToListAsync(cancellationToken);
}
