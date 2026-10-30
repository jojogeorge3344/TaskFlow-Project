using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Interfaces.Settings.ApplicationSettings;

/// <summary>The key/value rows of the application settings table.</summary>
public interface IApplicationSettingsRepository : IBaseRepository<ApplicationSetting>
{
    /// <summary>The stored values of the given keys, read-only. Keys with no row are absent.</summary>
    Task<IReadOnlyDictionary<string, string>> GetValuesAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    /// <summary>The stored value of one key, or null when it has no row.</summary>
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>The tracked rows of the given keys, for changing them. Keys with no row are absent.</summary>
    Task<IReadOnlyDictionary<string, ApplicationSetting>> GetForUpdateAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
}
