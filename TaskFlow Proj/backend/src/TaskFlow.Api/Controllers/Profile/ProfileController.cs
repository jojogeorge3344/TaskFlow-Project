using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Dtos.Auth.Mfa;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Dtos.Profile;
using TaskFlow.Api.Services.Auth.Mfa;
using TaskFlow.Api.Services.Interfaces.Profile;

namespace TaskFlow.Api.Controllers.Profile;

/// <summary>The caller's own account (Screen 19): details, password and authenticator MFA. The id always comes from the
/// token, so a user can never read or change somebody else's account here.</summary>
[ApiController]
[Route("api/v1/users/me")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    public async Task<ActionResult<UserDto>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _profileService.GetAsync(userId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Updates the caller's own full name and email. Role, project, username, employee
    /// code and account status are not part of this contract and cannot change here.</summary>
    [HttpPut]
    public async Task<ActionResult<UserDto>> Update([FromBody] UpdateOwnProfileRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _profileService.UpdateAsync(userId, request, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Changes the caller's own password after checking an authenticator code. Every session of
    /// the user is ended, so the client must sign in again with the new password.</summary>
    [HttpPost("password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _profileService.ChangePasswordAsync(userId, request, cancellationToken);
        return user is null ? NotFound() : NoContent();
    }

    /// <summary>Starts setup after the current password: returns the key and QR-code URI once. MFA stays off until confirmed.</summary>
    [HttpPost("mfa/setup")]
    public async Task<ActionResult<MfaSetupResponse>> SetupMfa([FromBody] MfaSetupRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _profileService.BeginMfaSetupAsync(userId, request.CurrentPassword!, cancellationToken);
        return result is null ? NotFound() : Ok(new MfaSetupResponse(result.Key, result.ProvisioningUri));
    }

    /// <summary>Confirms setup with the first code, turns MFA on and returns the backup codes, once.</summary>
    [HttpPost("mfa/enable")]
    public async Task<ActionResult<MfaBackupCodesResponse>> EnableMfa([FromBody] MfaEnableRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        var codes = await _profileService.EnableMfaAsync(userId, request.Code!, cancellationToken);
        return codes is null ? NotFound() : Ok(new MfaBackupCodesResponse(codes));
    }

    /// <summary>Checks a code without spending it. The Profile screen uses it before it will show what was typed into a password
    /// box. Wrong codes count toward the same lockout as sign-in.</summary>
    [HttpPost("mfa/check")]
    public async Task<ActionResult> CheckMfa([FromBody] MfaEnableRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        return await _profileService.CheckMfaCodeAsync(userId, request.Code, cancellationToken) switch
        {
            MfaVerifyOutcome.Success => NoContent(),
            MfaVerifyOutcome.LockedOut => Problem(detail: MfaService.LockedMessage, statusCode: StatusCodes.Status429TooManyRequests),
            _ => Problem(detail: MfaService.InvalidCodeMessage, statusCode: StatusCodes.Status400BadRequest)
        };
    }

    /// <summary>Replaces the backup codes (the old set stops working). Needs the current password and a valid code.</summary>
    [HttpPost("mfa/backup-codes")]
    public async Task<ActionResult<MfaBackupCodesResponse>> RegenerateBackupCodes([FromBody] MfaPasswordCodeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        var codes = await _profileService.RegenerateBackupCodesAsync(userId, request.CurrentPassword!, request.Code!, cancellationToken);
        return codes is null ? NotFound() : Ok(new MfaBackupCodesResponse(codes));
    }

    private bool TryGetCallerId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
