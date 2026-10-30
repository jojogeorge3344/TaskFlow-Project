using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Interfaces.Auth;

/// <summary>The refresh tokens that keep sessions alive.</summary>
public interface IRefreshTokenRepository : IBaseRepository<RefreshToken>
{
    /// <summary>The tracked, unrevoked tokens of these users, for revoking them.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveForUsersForUpdateAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);
}
