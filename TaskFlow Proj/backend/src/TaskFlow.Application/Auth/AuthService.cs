using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Mfa;
using Microsoft.Extensions.Options;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IMfaService _mfaService;
    private readonly IMfaSecretProtector _mfaProtector;
    private readonly MfaOptions _mfaOptions;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        IApplicationDbContext db,
        ITokenService tokenService,
        IPasswordHasher<User> passwordHasher,
        TimeProvider timeProvider,
        IMfaService mfaService,
        IMfaSecretProtector mfaProtector,
        IOptions<MfaOptions> mfaOptions)
    {
        _mfaService = mfaService;
        _mfaProtector = mfaProtector;
        _mfaOptions = mfaOptions.Value;
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

        if (user.MfaEnabledAt is not null)
        {
            // The password alone is not enough: hand back a short-lived challenge and issue no session yet.
            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            return AuthResult.RequiresMfa(_mfaProtector.CreateChallenge(
                user.Id, user.SecurityStamp, TimeSpan.FromMinutes(_mfaOptions.ChallengeMinutes)));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.LastLoginAt = now;
        user.UpdatedAt = now;

        var tokens = IssueTokens(user, now);

        await _db.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(tokens, await ToDtoAsync(user, cancellationToken));
    }

    public async Task<MfaSignInResult> VerifyMfaAsync(string? challengeToken, string? code, CancellationToken cancellationToken = default)
    {
        var invalid = new MfaSignInResult(AuthResult.Failure(), MfaVerifyOutcome.Invalid);

        var challenge = _mfaProtector.ReadChallenge(challengeToken);
        if (challenge is null)
        {
            return invalid;
        }

        var user = await _db.Users.Include(candidate => candidate.Role)
            .FirstOrDefaultAsync(candidate => candidate.Id == challenge.Value.UserId, cancellationToken);

        // A rotated stamp (password change, reset, admin action) kills any challenge issued before it.
        if (user is null || !user.IsActive || user.MfaEnabledAt is null || user.SecurityStamp != challenge.Value.SecurityStamp)
        {
            return invalid;
        }

        var outcome = await _mfaService.VerifyAsync(user, code, allowBackupCode: true, cancellationToken: cancellationToken);
        if (outcome != MfaVerifyOutcome.Success)
        {
            return new MfaSignInResult(AuthResult.Failure(), outcome);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.LastLoginAt = now;
        user.UpdatedAt = now;

        var tokens = IssueTokens(user, now);
        await _db.SaveChangesAsync(cancellationToken);

        return new MfaSignInResult(AuthResult.Success(tokens, await ToDtoAsync(user, cancellationToken)), MfaVerifyOutcome.Success);
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
            permissions,
            _mfaOptions.Required && user.MfaEnabledAt is null);
    }
}
