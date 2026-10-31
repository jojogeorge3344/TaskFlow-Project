using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Auth.PasswordRecovery;

/// <summary>The single-use tokens behind "forgot password", stored as hashes.</summary>
public interface IPasswordResetGrantRepository : IBaseRepository<PasswordResetGrant>
{
    /// <summary>The tracked unused grant with this hash, with its user.</summary>
    Task<PasswordResetGrant?> FindUnusedByHashWithUserForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>The tracked unused grants of this user other than <paramref name="exceptGrantId"/>.</summary>
    Task<IReadOnlyList<PasswordResetGrant>> GetOtherUnusedForUpdateAsync(int userId, int exceptGrantId, CancellationToken cancellationToken = default);
}
