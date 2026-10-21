using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.Cookies;
using TaskFlow.Application.Auth;
using TaskFlow.Application.PasswordReset;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<LoginRequest> _loginRequestValidator;
    private readonly IValidator<PasswordResetSubmission> _submissionValidator;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IValidator<PasswordResetCompleteRequest> _completeValidator;

    public AuthController(
        IAuthService authService,
        IValidator<LoginRequest> loginRequestValidator,
        IValidator<PasswordResetSubmission> submissionValidator,
        IPasswordResetService passwordResetService,
        IValidator<PasswordResetCompleteRequest> completeValidator)
    {
        _authService = authService;
        _loginRequestValidator = loginRequestValidator;
        _submissionValidator = submissionValidator;
        _passwordResetService = passwordResetService;
        _completeValidator = completeValidator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(Program.PasswordAttemptsPolicy)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _loginRequestValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var result = await _authService.LoginAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            return Unauthorized();
        }

        SetRefreshCookie(result.Tokens!);

        return Ok(ToResponse(result));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie.Name];

        var result = await _authService.RefreshAsync(refreshToken, cancellationToken);

        if (!result.Succeeded)
        {
            ClearRefreshCookie();
            return Unauthorized();
        }

        SetRefreshCookie(result.Tokens!);

        return Ok(ToResponse(result));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie.Name];

        await _authService.LogoutAsync(refreshToken, cancellationToken);

        ClearRefreshCookie();

        return NoContent();
    }

    /// <summary>Forgot Password on the Login Screen. The identifier is the registered email address or
    /// username of the account that needs recovery. The answer is the same generic confirmation whether
    /// or not an account matched, so this never discloses whether an account exists.</summary>
    [HttpPost("password-reset-requests")]
    [AllowAnonymous]
    public async Task<IActionResult> CreatePasswordResetRequest(
        [FromBody] PasswordResetSubmission submission,
        CancellationToken cancellationToken)
    {
        var validation = await _submissionValidator.ValidateAsync(submission, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        await _passwordResetService.CreateRequestAsync(
            sessionUserId: null,
            identifier: submission.Identifier,
            origin: PasswordResetOrigin.LoginScreen,
            cancellationToken);

        return Ok(new { message = PasswordResetTexts.Confirmation });
    }

    /// <summary>Resolves an encrypted login-link token: the email to pre-fill and whether the notice is waiting.
    /// A bad, expired or superseded token answers exactly like an unknown account (no email, no notice), so the
    /// address reveals nothing and the identifier in it is never readable (design D14).</summary>
    [HttpGet("password-reset-link")]
    [AllowAnonymous]
    public async Task<IActionResult> ResolvePasswordResetLink([FromQuery] string? token, CancellationToken cancellationToken)
    {
        var email = await _passwordResetService.ResolveLoginLinkAsync(token, cancellationToken);
        var noticeAvailable = email is not null && await _authService.HasPasswordResetNoticeAsync(email, cancellationToken);
        return Ok(new { email, noticeAvailable });
    }

    /// <summary>The password update behind the bell on the Login Screen: the temporary password the administrator
    /// issued proves who the user is, then the new password replaces it. Every refusal is the same generic 401, so a
    /// wrong password, an unknown account and a bad token cannot be told apart, and the attempts are rate limited.</summary>
    [HttpPost("complete-password-reset")]
    [AllowAnonymous]
    [EnableRateLimiting(Program.PasswordAttemptsPolicy)]
    public async Task<IActionResult> CompletePasswordReset(
        [FromBody] PasswordResetCompleteRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _completeValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var updated = await _passwordResetService.CompleteFromLoginAsync(request, cancellationToken);
        return updated
            ? NoContent()
            : Unauthorized(new { error = "The temporary password was not accepted." });
    }

    /// <summary>The Login Screen's credential-notice read for the identifier being typed. Always 200 with the
    /// same single field, so an unknown account looks exactly like an account without a notice.</summary>
    [HttpGet("password-reset-notice")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPasswordResetNotice([FromQuery] string? identifier, CancellationToken cancellationToken)
    {
        var noticeAvailable = await _authService.HasPasswordResetNoticeAsync(identifier, cancellationToken);
        return Ok(new { noticeAvailable });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthenticatedUserResponse>> Me(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetCurrentAsync(userId, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new AuthenticatedUserResponse(
            user.Id,
            user.FullName,
            user.Username,
            user.Email,
            user.Role,
            user.EmployeeCode,
            user.Permissions));
    }

    private void SetRefreshCookie(AuthTokens tokens)
    {
        Response.Cookies.Append(
            RefreshTokenCookie.Name,
            tokens.RefreshToken,
            RefreshTokenCookie.CreateOptions(tokens.RefreshTokenExpiresAt));
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookie.Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshTokenCookie.Path
        });
    }

    private static AuthResponse ToResponse(AuthResult result) => new(
        result.Tokens!.AccessToken,
        result.Tokens.AccessTokenExpiresAt,
        new AuthenticatedUserResponse(
            result.User!.Id,
            result.User.FullName,
            result.User.Username,
            result.User.Email,
            result.User.Role,
            result.User.EmployeeCode,
            result.User.Permissions));
}
