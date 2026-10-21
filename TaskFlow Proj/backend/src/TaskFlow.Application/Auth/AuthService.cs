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

        var user = await _db.Users.Include(candidate => candidate.Role).FirstOrDefaultAsync(
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

        // A successful sign-in - and only that - clears the account's Login Screen notice (design D2).
        var notice = await _db.LoginPasswordResetNotices
            .FirstOrDefaultAsync(existing => existing.UserId == user.Id && existing.ClearedAt == null, cancellationToken);
        if (notice is not null)
        {
            notice.ClearedAt = now;
        }

        var tokens = IssueTokens(user, now);

        await _db.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(tokens, await ToDtoAsync(user, cancellationToken));
    }

    public async Task<bool> HasPasswordResetNoticeAsync(string? identifier, CancellationToken cancellationToken = default)
    {
        var trimmed = (identifier ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        // The notice has no time limit: it is there until a sign-in or a password update clears it (design D12).
        return await _db.LoginPasswordResetNotices
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.ClearedAt == null
                    && candidate.User!.IsActive
                    && (candidate.User.Username == trimmed || candidate.User.Email == trimmed),
                cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthResult.Failure();
        }

        var hash = _tokenService.HashRefreshToken(refreshToken);

        var stored = await _db.RefreshTokens
            .Include(token => token.User).ThenInclude(user => user!.Role)
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

        return AuthResult.Success(tokens, await ToDtoAsync(stored.User, cancellationToken));
    }

    public async System.Threading.Tasks.Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
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
        var user = await _db.Users.Include(candidate => candidate.Role).FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        return user is null ? null : await ToDtoAsync(user, cancellationToken);
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

    private async Task<UserDto> ToDtoAsync(User user, CancellationToken cancellationToken)
    {
        var permissions = await _db.RolePermissions
            .AsNoTracking()
            .Where(grant => grant.RoleId == user.RoleId)
            .Select(grant => grant.Permission.Key)
            .OrderBy(key => key)
            .ToListAsync(cancellationToken);

        return new UserDto(
            user.Id,
            user.FullName,
            user.Username,
            user.Email,
            user.Role.Name,
            user.EmployeeCode,
            permissions);
    }
}
