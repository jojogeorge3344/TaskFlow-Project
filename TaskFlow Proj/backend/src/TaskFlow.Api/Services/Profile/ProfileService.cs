using Microsoft.AspNetCore.Identity;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Dtos.Auth.Mfa;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Dtos.Profile;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Repositories.Auth;
using TaskFlow.Api.Repositories.Management.EmployeeManagement;
using TaskFlow.Api.Services.Auth;
using TaskFlow.Api.Services.Auth.Mfa;

namespace TaskFlow.Api.Services.Profile;

public class ProfileService : IProfileService
{
    public const string MfaRequiredForPasswordMessage = "Set up two-step verification before changing your password.";
    public const string TooManyAttemptsMessage = "Too many attempts. Try again in a few minutes.";

    private readonly IEmployeeRepository _employees;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IMfaService _mfaService;
    private readonly IAttemptLimiter _limiter;

    public ProfileService(
        IEmployeeRepository employees,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher,
        IMfaService mfaService,
        IAttemptLimiter limiter)
    {
        _employees = employees;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _mfaService = mfaService;
        _limiter = limiter;
    }

    public Task<UserDto?> GetAsync(int userId, CancellationToken cancellationToken = default) =>
        _employees.GetDtoAsync(userId, cancellationToken);

    public async Task<UserDto?> UpdateAsync(int userId, UpdateOwnProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _employees.GetActiveWithRoleForUpdateAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var email = EmailPolicy.Normalize(request.Email);
        if (!EmailPolicy.IsValid(email))
        {
            throw new BusinessRuleException(EmailPolicy.InvalidMessage);
        }

        // Null keeps the current number; an empty value clears it.
        var mobileNumber = request.MobileNumber is null
            ? user.MobileNumber
            : MobileNumberPolicy.Normalize(request.MobileNumber);
        if (mobileNumber is not null && !MobileNumberPolicy.IsValid(mobileNumber))
        {
            throw new BusinessRuleException(MobileNumberPolicy.InvalidMessage);
        }

        // Email is the only unique identity, so a number may be shared. What is refused is a second record that already holds
        // this exact email and number together.
        if (mobileNumber is not null && await _employees.EmailAndMobileTakenAsync(email, mobileNumber, userId, cancellationToken))
        {
            throw new BusinessRuleException("A user with this email and mobile number already exists.");
        }

        // The domain is fixed, so two addresses are the same person's mailbox when their local parts match. Deleted users
        // still count: their address stays reserved so history is never ambiguous.
        if (await _employees.EmailPrefixTakenAsync(EmailPolicy.LocalPart(email) + "@", userId, cancellationToken))
        {
            throw new BusinessRuleException("Email already exists.");
        }

        user.FullName = request.FullName!.Trim();
        user.Email = email;
        user.MobileNumber = mobileNumber;
        user.UpdatedAt = DateTime.UtcNow;

        // Role, project, username, employee code and status are deliberately untouched.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto?> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(userId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        // The authenticator code is the proof of identity. RequireCodeAsync passes silently when MFA is off, which the setup
        // gate prevents; refuse here too so a gate regression cannot leave the password change unprotected.
        if (!current.MfaEnabled)
        {
            throw new BusinessRuleException(MfaRequiredForPasswordMessage);
        }

        await _mfaService.RequireCodeAsync(userId, request.MfaCode, cancellationToken);

        var user = await _employees.GetActiveWithRoleForUpdateAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword!);
        user.UpdatedAt = now;

        // The password change ends every session, including this one; the client signs in again. Access tokens carrying the
        // old stamp fail validation, and the refresh tokens are revoked so no new ones can be minted.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        foreach (var token in await _refreshTokens.GetActiveForUsersForUpdateAsync([user.Id], cancellationToken))
        {
            token.RevokedAt = now;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(user.Id, cancellationToken);
    }

    public async Task<MfaSetupResult?> BeginMfaSetupAsync(int userId, string currentPassword, CancellationToken cancellationToken = default)
    {
        // The current password is a guess an access token alone could repeat, so it has a budget of its own.
        var passwordKey = AcquirePasswordAttempt(userId);
        var result = await _mfaService.BeginSetupAsync(userId, currentPassword, cancellationToken);
        _limiter.Reset(passwordKey);
        return result;
    }

    public Task<IReadOnlyList<string>?> EnableMfaAsync(int userId, string code, CancellationToken cancellationToken = default) =>
        _mfaService.EnableAsync(userId, code, cancellationToken);

    public Task<MfaVerifyOutcome> CheckMfaCodeAsync(int userId, string? code, CancellationToken cancellationToken = default) =>
        _mfaService.CheckCodeAsync(userId, code, cancellationToken);

    public async Task<IReadOnlyList<string>?> RegenerateBackupCodesAsync(
        int userId,
        string currentPassword,
        string code,
        CancellationToken cancellationToken = default)
    {
        var passwordKey = AcquirePasswordAttempt(userId);
        var codes = await _mfaService.RegenerateBackupCodesAsync(userId, currentPassword, code, cancellationToken);
        _limiter.Reset(passwordKey);
        return codes;
    }

    private string AcquirePasswordAttempt(int userId)
    {
        var passwordKey = IAttemptLimiter.PasswordKey(userId);
        if (!_limiter.TryAcquire(passwordKey, remoteAddress: null))
        {
            throw new RateLimitedException(TooManyAttemptsMessage);
        }

        return passwordKey;
    }
}
