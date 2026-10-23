using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Auth;
using TaskFlow.Application.Mfa;

namespace TaskFlow.Api.Controllers;

/// <summary>Password recovery from the login page with an authenticator code: the code buys a single-use reset token, and
/// the token sets the new password. No password or code is ever sent by SMS or email.</summary>
[ApiController]
[Route("api/v1/auth/password-recovery")]
public class PasswordRecoveryController : ControllerBase
{
    // One message for every failure, so the response never says whether the account exists or has MFA on.
    private const string FailureMessage = "The details are incorrect, or the code has expired. Check them and try again.";
    private const string ThrottledMessage = "Too many attempts. Try again in a few minutes.";

    private readonly IApplicationDbContext _db;
    private readonly IMfaService _mfaService;
    private readonly IPasswordRecoveryService _recoveryService;
    private readonly IRecoveryAttemptLimiter _limiter;
    private readonly IValidator<AuthenticatorRecoveryRequest> _verifyValidator;
    private readonly IValidator<RecoverPasswordRequest> _recoverValidator;

    public PasswordRecoveryController(
        IApplicationDbContext db,
        IMfaService mfaService,
        IPasswordRecoveryService recoveryService,
        IRecoveryAttemptLimiter limiter,
        IValidator<AuthenticatorRecoveryRequest> verifyValidator,
        IValidator<RecoverPasswordRequest> recoverValidator)
    {
        _db = db;
        _mfaService = mfaService;
        _recoveryService = recoveryService;
        _limiter = limiter;
        _verifyValidator = verifyValidator;
        _recoverValidator = recoverValidator;
    }

    /// <summary>Step 1: the account and a current authenticator code. A correct code returns the single-use reset token.</summary>
    [HttpPost("authenticator/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<RecoveryTokenResponse>> VerifyAuthenticator(
        [FromBody] AuthenticatorRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _verifyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var identifier = request.Identifier!.Trim();
        var address = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Recovery has its own budget per account and per address, and wrong codes here never touch the account's sign-in
        // lockout, so a stranger who only knows a username cannot lock the owner out. Every refusal looks the same.
        if (_limiter.IsBlocked(identifier, address))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = ThrottledMessage });
        }

        var user = await _db.Users.FirstOrDefaultAsync(
            candidate => (candidate.Username == identifier || candidate.Email == identifier)
                && candidate.IsActive && !candidate.IsDeleted,
            cancellationToken);

        if (user is null || user.MfaEnabledAt is null)
        {
            _limiter.RecordFailure(identifier, address);
            return BadRequest(new { error = FailureMessage });
        }

        // An unused backup code is not a recovery factor. The account's own lock still applies.
        var outcome = await _mfaService.VerifyAsync(user, request.Code, allowBackupCode: false, countFailure: false, cancellationToken: cancellationToken);
        if (outcome != MfaVerifyOutcome.Success)
        {
            _limiter.RecordFailure(identifier, address);
            return BadRequest(new { error = FailureMessage });
        }

        _limiter.Reset(identifier);
        return Ok(new RecoveryTokenResponse(await _recoveryService.IssueResetTokenAsync(user.Id, cancellationToken)));
    }

    /// <summary>Step 2: sets the new password with the reset token. A rejected password leaves the token usable until it expires.</summary>
    [HttpPost("reset")]
    [AllowAnonymous]
    public async Task<IActionResult> Reset([FromBody] RecoverPasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await _recoverValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var reset = await _recoveryService.ResetPasswordAsync(request.ResetToken!, request.NewPassword!, cancellationToken);
        return reset
            ? NoContent()
            : BadRequest(new { error = "This reset link is no longer valid. Start again from the login page." });
    }
}
