using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Dtos.Auth.PasswordRecovery;
using TaskFlow.Api.Services.Auth.PasswordRecovery;

namespace TaskFlow.Api.Controllers.Auth;

/// <summary>Password recovery from the login page with an authenticator code: the code buys a single-use reset token, and
/// the token sets the new password. No password or code is ever sent by SMS or email.</summary>
[ApiController]
[Route("api/v1/auth/password-recovery")]
public class PasswordRecoveryController : ControllerBase
{
    // One message for every failure, so the response never says whether the account exists or has MFA on.
    private const string FailureMessage = "The details are incorrect, or the code has expired. Check them and try again.";
    private const string ThrottledMessage = "Too many attempts. Try again in a few minutes.";

    private readonly IPasswordRecoveryService _recoveryService;

    public PasswordRecoveryController(IPasswordRecoveryService recoveryService)
    {
        _recoveryService = recoveryService;
    }

    /// <summary>Step 1: the account and a current authenticator code. A correct code returns the single-use reset token.</summary>
    [HttpPost("authenticator/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<RecoveryTokenResponse>> VerifyAuthenticator(
        [FromBody] AuthenticatorRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _recoveryService.VerifyAuthenticatorAsync(
            request.Identifier!,
            request.Code,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return result.Status switch
        {
            RecoveryVerifyStatus.Verified => Ok(new RecoveryTokenResponse(result.ResetToken!)),
            RecoveryVerifyStatus.Throttled => Problem(detail: ThrottledMessage, statusCode: StatusCodes.Status429TooManyRequests),
            _ => Problem(detail: FailureMessage, statusCode: StatusCodes.Status400BadRequest)
        };
    }

    /// <summary>Step 2: sets the new password with the reset token. A rejected password leaves the token usable until it expires.</summary>
    [HttpPost("reset")]
    [AllowAnonymous]
    public async Task<IActionResult> Reset([FromBody] RecoverPasswordRequest request, CancellationToken cancellationToken)
    {
        var reset = await _recoveryService.ResetPasswordAsync(request.ResetToken!, request.NewPassword!, cancellationToken);
        return reset
            ? NoContent()
            : Problem(detail: "This reset link is no longer valid. Start again from the login page.", statusCode: StatusCodes.Status400BadRequest);
    }
}
