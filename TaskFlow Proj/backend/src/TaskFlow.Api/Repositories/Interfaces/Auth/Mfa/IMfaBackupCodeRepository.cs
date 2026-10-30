using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Interfaces.Auth.Mfa;

/// <summary>The single-use backup codes of authenticator MFA, stored as hashes.</summary>
public interface IMfaBackupCodeRepository : IBaseRepository<MfaBackupCode>
{
    /// <summary>The tracked unused code of this user with this hash.</summary>
    Task<MfaBackupCode?> FindUnusedForUpdateAsync(int userId, string codeHash, CancellationToken cancellationToken = default);

    /// <summary>Every tracked code of this user, used or not.</summary>
    Task<IReadOnlyList<MfaBackupCode>> GetForUserForUpdateAsync(int userId, CancellationToken cancellationToken = default);
}
