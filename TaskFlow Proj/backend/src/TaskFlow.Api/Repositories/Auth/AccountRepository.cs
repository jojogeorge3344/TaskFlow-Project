using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Repositories.Auth;

public class AccountRepository : BaseRepository<User>, IAccountRepository
{
    public AccountRepository(AppDbContext db)
        : base(db)
    {
    }

    public Task<User?> FindByLoginForUpdateAsync(string identifier, CancellationToken cancellationToken = default) =>
        Set.Include(candidate => candidate.Role).FirstOrDefaultAsync(
            candidate => candidate.Username == identifier || candidate.Email == identifier,
            cancellationToken);

    public Task<User?> FindActiveByLoginForUpdateAsync(string identifier, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(
            candidate => (candidate.Username == identifier || candidate.Email == identifier)
                && candidate.IsActive && !candidate.IsDeleted,
            cancellationToken);

    public Task<User?> FindWithRoleForUpdateAsync(int userId, CancellationToken cancellationToken = default) =>
        Set.Include(candidate => candidate.Role).FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

    public Task<User?> FindLiveForUpdateAsync(int userId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<string>> GetPermissionKeysAsync(int roleId, CancellationToken cancellationToken = default) =>
        await Db.RolePermissions
            .AsNoTracking()
            .Where(grant => grant.RoleId == roleId)
            .Select(grant => grant.Permission.Key)
            .ToListAsync(cancellationToken);

    public Task<SessionUser?> GetSessionAsync(int userId, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new SessionUser(
                candidate.SecurityStamp,
                candidate.IsActive,
                candidate.MfaEnabledAt,
                candidate.Role.Name,
                candidate.Role.Permissions.Select(grant => grant.Permission.Key).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
}
