using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Repositories.Auth.Mfa;

public class MfaBackupCodeRepository : BaseRepository<MfaBackupCode>, IMfaBackupCodeRepository
{
    public MfaBackupCodeRepository(AppDbContext db)
        : base(db)
    {
    }

    public Task<MfaBackupCode?> FindUnusedForUpdateAsync(int userId, string codeHash, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(row => row.UserId == userId && row.UsedAt == null && row.CodeHash == codeHash, cancellationToken);

    public async Task<IReadOnlyList<MfaBackupCode>> GetForUserForUpdateAsync(int userId, CancellationToken cancellationToken = default) =>
        await Set.Where(row => row.UserId == userId).ToListAsync(cancellationToken);
}
