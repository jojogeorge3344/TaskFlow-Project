using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

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

    public Task<AiProviderConfig?> GetAsync(AiProviderKind kind, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(config => config.Kind == kind, cancellationToken);

    public Task<bool> UsesModelAsync(AiProviderKind kind, string modelName, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(config => config.Kind == kind && config.ModelName == modelName, cancellationToken);
}
