using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        IApplicationDbContext db,
        ITokenService tokenService,
        IPasswordHasher<User> passwordHasher,
        TimeProvider timeProvider)
    {
        _db = db;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var identifier = request.EmailOrUsername.Trim();

        var user = await _db.Users.FirstOrDefaultAsync(
            candidate => candidate.Username == identifier || candidate.Email == identifier,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            return AuthResult.Failure();
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return AuthResult.Failure();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.LastLoginAt = now;
        user.UpdatedAt = now;

        var tokens = IssueTokens(user, now);

        await _db.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(tokens, ToDto(user));
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthResult.Failure();
        }

        var hash = _tokenService.HashRefreshToken(refreshToken);

        var stored = await _db.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (stored is null ||
            stored.RevokedAt is not null ||
            stored.ExpiresAt <= now ||
            stored.User is null ||
            !stored.User.IsActive)
        {
            return AuthResult.Failure();
        }

        stored.RevokedAt = now;

        var tokens = IssueTokens(stored.User, now);

        await _db.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(tokens, ToDto(stored.User));
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = _tokenService.HashRefreshToken(refreshToken);

        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDto?> GetCurrentAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        return user is null ? null : ToDto(user);
    }

    private AuthTokens IssueTokens(User user, DateTime now)
    {
        var accessToken = _tokenService.CreateAccessToken(user);
        var refreshToken = _tokenService.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshToken.TokenHash,
            ExpiresAt = refreshToken.ExpiresAt,
            CreatedAt = now
        });

        return new AuthTokens(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.Token,
            refreshToken.ExpiresAt);
    }

    private static UserDto ToDto(User user) => new(
        user.Id,
        user.FullName,
        user.Username,
        user.Email,
        user.Role.ToString(),
        user.EmployeeCode);
}
