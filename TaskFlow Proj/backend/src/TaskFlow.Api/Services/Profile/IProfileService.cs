using TaskFlow.Api.Dtos.Auth.Mfa;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Dtos.Profile;

namespace TaskFlow.Api.Services.Profile;

/// <summary>The signed-in user's own account: details, password and authenticator MFA. Every method acts on the caller's
/// own id only, so nobody can read or change another user's account here.</summary>
public interface IProfileService
{
    Task<UserDto?> GetAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Updates the caller's own full name, email and mobile number. Nothing else is changeable.</summary>
    Task<UserDto?> UpdateAsync(int userId, UpdateOwnProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes the caller's own password after a valid authenticator code and ends every session. Refused when
    /// the account has no authenticator MFA.</summary>
    Task<UserDto?> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Starts authenticator setup after the current password, which has an attempt budget of its own.</summary>
    Task<MfaSetupResult?> BeginMfaSetupAsync(int userId, string currentPassword, CancellationToken cancellationToken = default);

    /// <summary>Confirms setup with the first code and returns the backup codes, once.</summary>
    Task<IReadOnlyList<string>?> EnableMfaAsync(int userId, string code, CancellationToken cancellationToken = default);

    /// <summary>Checks a code without spending it.</summary>
    Task<MfaVerifyOutcome> CheckMfaCodeAsync(int userId, string? code, CancellationToken cancellationToken = default);

    /// <summary>Replaces the backup codes after the current password and a valid code.</summary>
    Task<IReadOnlyList<string>?> RegenerateBackupCodesAsync(int userId, string currentPassword, string code, CancellationToken cancellationToken = default);
}
