using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Settings.RoleConfiguration.Permissions;

public class PermissionRepository : BaseRepository<Permission>, IPermissionRepository
{
    public PermissionRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyDictionary<string, int>> GetIdsByKeyAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().ToDictionaryAsync(permission => permission.Key, permission => permission.Id, cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        await Set.Where(permission => keys.Contains(permission.Key)).ToListAsync(cancellationToken);
}
