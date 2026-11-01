using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;

public class AiProviderConfigRepository : BaseRepository<AiProviderConfig>, IAiProviderConfigRepository
{
    public AiProviderConfigRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<AiProviderConfig>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().ToListAsync(cancellationToken);

    public Task<List<AiProviderConfig>> GetAllForUpdateAsync(CancellationToken cancellationToken = default) =>
        Set.ToListAsync(cancellationToken);

    public Task<AiProviderConfig?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(config => config.Id == id, cancellationToken);

    public Task<AiProviderConfig?> GetBuiltInAsync(AiApiType apiType, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(config => config.IsBuiltIn && config.ApiType == apiType, cancellationToken);

    // Name has the NOCASE collation, so this comparison ignores case in SQLite.
    public Task<AiProviderConfig?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(config => config.Name == name, cancellationToken);
}
