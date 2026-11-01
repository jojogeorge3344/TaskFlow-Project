using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Settings.TfaSettings.AiModels;

public class AiModelRepository : BaseRepository<AiModel>, IAiModelRepository
{
    public AiModelRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<AiModel>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .OrderByDescending(model => model.IsBuiltIn)
            .ThenBy(model => model.Id)
            .ToListAsync(cancellationToken);

    public async Task<bool> IsKnownAsync(int providerId, string name, CancellationToken cancellationToken = default) =>
        Set.Local.Any(model => model.ProviderId == providerId && model.Name == name)
        || await Set.AnyAsync(model => model.ProviderId == providerId && model.Name == name, cancellationToken);

    public Task<AiModel?> GetForUpdateAsync(int providerId, string name, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(model => model.ProviderId == providerId && model.Name == name, cancellationToken);

    public async Task<bool> DropAddedModelsStoredElsewhereAsync(CancellationToken cancellationToken = default)
    {
        var dropped = false;
        foreach (var entry in Db.ChangeTracker.Entries<AiModel>().Where(entry => entry.State == EntityState.Added).ToList())
        {
            var model = entry.Entity;
            if (await Set.AsNoTracking().AnyAsync(stored => stored.ProviderId == model.ProviderId && stored.Name == model.Name, cancellationToken))
            {
                entry.State = EntityState.Detached;
                dropped = true;
            }
        }

        return dropped;
    }
}
