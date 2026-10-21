namespace TaskFlow.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);

    Task<UserDto?> GetCurrentAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Whether the account the typed identifier belongs to has an uncleared Login Screen notice for a
    /// completed reset. The answer is a bare boolean - false for an unknown, inactive or notice-less account alike -
    /// so it never discloses anything else about the account.</summary>
    Task<bool> HasPasswordResetNoticeAsync(string? identifier, CancellationToken cancellationToken = default);
}
