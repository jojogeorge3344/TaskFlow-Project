using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Settings.RoleConfiguration.Permissions;

/// <summary>The stored permission rows. The catalogue in code defines them; the table only supplies the ids grants use.</summary>
public interface IPermissionRepository : IBaseRepository<Permission>
{
    Task<IReadOnlyDictionary<string, int>> GetIdsByKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>The tracked rows of the given keys, to grant them. Unknown keys are absent.</summary>
    Task<IReadOnlyList<Permission>> GetByKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
}
