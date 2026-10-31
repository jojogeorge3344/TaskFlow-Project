using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Config;
using TaskFlow.Api.Dtos.Auth;
using TaskFlow.Api.Dtos.Auth.Mfa;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Services.Auth.Mfa;
using TaskFlow.Api.Integrations.Security.MfaSecrets;
using TaskFlow.Api.Integrations.Security.Tokens;
using TaskFlow.Api.Repositories.Auth.Accounts;
using TaskFlow.Api.Repositories.Auth.RefreshTokens;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Services.Auth.Login;

public class AuthService : IAuthService
{
    private readonly IAccountRepository _accounts;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IMfaService _mfaService;
    private readonly IMfaSecretProtector _mfaProtector;
    private readonly MfaOptions _mfaOptions;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        IAccountRepository accounts,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        IPasswordHasher<User> passwordHasher,
        TimeProvider timeProvider,
        IMfaService mfaService,
        IMfaSecretProtector mfaProtector,
        IOptions<MfaOptions> mfaOptions)
    {
        _accounts = accounts;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _mfaService = mfaService;
        _mfaProtector = mfaProtector;
        _mfaOptions = mfaOptions.Value;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _accounts.FindByLoginForUpdateAsync(request.EmailOrUsername.Trim(), cancellationToken);

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
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return AuthResult.RequiresMfa(_mfaProtector.CreateChallenge(
                user.Id, user.SecurityStamp, TimeSpan.FromMinutes(_mfaOptions.ChallengeMinutes)));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.LastLoginAt = now;
        user.UpdatedAt = now;

        var tokens = IssueTokens(user, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

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

        var user = await _accounts.FindWithRoleForUpdateAsync(challenge.Value.UserId, cancellationToken);

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
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MfaSignInResult(AuthResult.Success(tokens, await ToDtoAsync(user, cancellationToken)), MfaVerifyOutcome.Success);
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthResult.Failure();
        }

        var stored = await _refreshTokens.FindByHashWithUserForUpdateAsync(
            _tokenService.HashRefreshToken(refreshToken),
            cancellationToken);

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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(tokens, await ToDtoAsync(stored.User, cancellationToken));
    }

    public async System.Threading.Tasks.Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var stored = await _refreshTokens.FindByHashForUpdateAsync(_tokenService.HashRefreshToken(refreshToken), cancellationToken);

        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDto?> GetCurrentAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _accounts.FindWithRoleForUpdateAsync(userId, cancellationToken);

        return user is null ? null : await ToDtoAsync(user, cancellationToken);
    }

    private AuthTokens IssueTokens(User user, DateTime now)
    {
        var accessToken = _tokenService.CreateAccessToken(user);
        var refreshToken = _tokenService.CreateRefreshToken();

        _refreshTokens.Add(new RefreshToken
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
        var permissions = TaskFlow.Api.Common.Security.EffectivePermissions
            .Expand(await _accounts.GetPermissionKeysAsync(user.RoleId, cancellationToken))
            .Order(StringComparer.Ordinal)
            .ToList();

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
