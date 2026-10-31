using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Auth.Accounts;

/// <summary>The accounts people sign in with. Deleted accounts are never found.</summary>
public interface IAccountRepository : IBaseRepository<User>
{
    /// <summary>The tracked user, with their role, whose username or email is <paramref name="identifier"/>.</summary>
    Task<User?> FindByLoginForUpdateAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>The tracked active user whose username or email is <paramref name="identifier"/>.</summary>
    Task<User?> FindActiveByLoginForUpdateAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>The tracked user with their role.</summary>
    Task<User?> FindWithRoleForUpdateAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The tracked user, if not deleted.</summary>
    Task<User?> FindLiveForUpdateAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The permission keys granted to the role, as stored (not expanded).</summary>
    Task<IReadOnlyList<string>> GetPermissionKeysAsync(int roleId, CancellationToken cancellationToken = default);

    Task<SessionUser?> GetSessionAsync(int userId, CancellationToken cancellationToken = default);
}
