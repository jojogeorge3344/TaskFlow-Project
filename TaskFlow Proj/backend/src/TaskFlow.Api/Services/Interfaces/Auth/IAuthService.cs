using TaskFlow.Api.Dtos.Auth;

namespace TaskFlow.Api.Services.Interfaces.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Second sign-in step for an account with MFA on: the challenge from <see cref="LoginAsync"/> plus an
    /// authenticator or backup code. Issues the session on success.</summary>
    Task<MfaSignInResult> VerifyMfaAsync(string? challengeToken, string? code, CancellationToken cancellationToken = default);

    Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);

    Task<UserDto?> GetCurrentAsync(int userId, CancellationToken cancellationToken = default);
}
