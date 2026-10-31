using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Settings.ApplicationSettings;

public class ApplicationSettingsRepository : BaseRepository<ApplicationSetting>, IApplicationSettingsRepository
{
    public ApplicationSettingsRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyDictionary<string, string>> GetValuesAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(setting => keys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

    public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking()
            .Where(setting => setting.Key == key)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, ApplicationSetting>> GetForUpdateAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default) =>
        await Set.Where(setting => keys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, cancellationToken);
}
