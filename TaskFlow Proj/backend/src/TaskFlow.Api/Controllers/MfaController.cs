using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Auth;
using TaskFlow.Application.Mfa;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.Api.Controllers;

/// <summary>Authenticator-app MFA for the signed-in user (setup, confirm, backup codes, turn off) and the
/// administrator reset. The account always comes from the token, so nobody can touch another user's MFA here.</summary>
[ApiController]
[Route("api/v1/users")]
public class MfaController : ControllerBase
{
    private const string TooManyAttemptsMessage = "Too many attempts. Try again in a few minutes.";

    private readonly IMfaService _mfaService;
    private readonly IAttemptLimiter _limiter;

    public MfaController(
        IMfaService mfaService,
        IAttemptLimiter limiter)
    {
        _mfaService = mfaService;
        _limiter = limiter;
    }

    /// <summary>Starts setup after the current password: returns the key and QR-code URI once. MFA stays off until confirmed.</summary>
    [HttpPost("me/mfa/setup")]
    [Authorize]
    public async Task<ActionResult<MfaSetupResponse>> Setup([FromBody] MfaSetupRequest request, CancellationToken cancellationToken)
    {
        return await RunAsync(async userId =>
        {
            // The current password is a guess an access token alone could repeat, so it has a budget of its own.
            var passwordKey = IAttemptLimiter.PasswordKey(userId);
            if (!_limiter.TryAcquire(passwordKey, remoteAddress: null))
            {
                return (ActionResult<MfaSetupResponse>)Problem(detail: TooManyAttemptsMessage, statusCode: StatusCodes.Status429TooManyRequests);
            }

            var result = await _mfaService.BeginSetupAsync(userId, request.CurrentPassword!, cancellationToken);
            _limiter.Reset(passwordKey);
            return result is null ? NotFound() : Ok(new MfaSetupResponse(result.Key, result.ProvisioningUri));
        });
    }

    /// <summary>Confirms setup with the first code, turns MFA on and returns the backup codes, once.</summary>
    [HttpPost("me/mfa/enable")]
    [Authorize]
    public async Task<ActionResult<MfaBackupCodesResponse>> Enable([FromBody] MfaEnableRequest request, CancellationToken cancellationToken)
    {
        return await RunAsync(async userId =>
        {
            var codes = await _mfaService.EnableAsync(userId, request.Code!, cancellationToken);
            return codes is null ? NotFound() : Ok(new MfaBackupCodesResponse(codes));
        });
    }

    /// <summary>Checks a code without spending it. The Profile screen uses it before it will show what was typed into a password
    /// box. Wrong codes count toward the same lockout as sign-in.</summary>
    [HttpPost("me/mfa/check")]
    [Authorize]
    public async Task<ActionResult> Check([FromBody] MfaEnableRequest request, CancellationToken cancellationToken)
    {
        return await RunAsync(async userId =>
        {
            var outcome = await _mfaService.CheckCodeAsync(userId, request.Code, cancellationToken);
            return outcome switch
            {
                MfaVerifyOutcome.Success => NoContent(),
                MfaVerifyOutcome.LockedOut => Problem(detail: MfaService.LockedMessage, statusCode: StatusCodes.Status429TooManyRequests),
                _ => (ActionResult)Problem(detail: MfaService.InvalidCodeMessage, statusCode: StatusCodes.Status400BadRequest)
            };
        });
    }

    /// <summary>Replaces the backup codes (the old set stops working). Needs the current password and a valid code.</summary>
    [HttpPost("me/mfa/backup-codes")]
    [Authorize]
    public async Task<ActionResult<MfaBackupCodesResponse>> RegenerateBackupCodes([FromBody] MfaPasswordCodeRequest request, CancellationToken cancellationToken)
    {
        return await RunAsync(async userId =>
        {
            var passwordKey = IAttemptLimiter.PasswordKey(userId);
            if (!_limiter.TryAcquire(passwordKey, remoteAddress: null))
            {
                return (ActionResult<MfaBackupCodesResponse>)Problem(detail: TooManyAttemptsMessage, statusCode: StatusCodes.Status429TooManyRequests);
            }

            var codes = await _mfaService.RegenerateBackupCodesAsync(userId, request.CurrentPassword!, request.Code!, cancellationToken);
            _limiter.Reset(passwordKey);
            return codes is null ? NotFound() : Ok(new MfaBackupCodesResponse(codes));
        });
    }

    /// <summary>Administrator reset for someone who lost their phone and their backup codes. Ends the user's sessions.</summary>
    [HttpPost("{id:int}/mfa/reset")]
    [RequirePermission(PermissionKeys.EmployeesMfaReset)]
    public async Task<ActionResult> Reset(int id, CancellationToken cancellationToken)
    {
        return await _mfaService.ResetAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    private async Task<ActionResult<T>> RunAsync<T>(Func<int, Task<ActionResult<T>>> action)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        return await action(userId);
    }

    private async Task<ActionResult> RunAsync(Func<int, Task<ActionResult>> action)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        return await action(userId);
    }
}
