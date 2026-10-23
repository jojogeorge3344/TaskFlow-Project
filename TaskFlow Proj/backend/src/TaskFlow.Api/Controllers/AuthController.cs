using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.Cookies;
using TaskFlow.Application.Auth;
using TaskFlow.Application.Mfa;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<LoginRequest> _loginRequestValidator;
    private readonly IValidator<MfaVerifyRequest> _mfaVerifyValidator;

    public AuthController(
        IAuthService authService,
        IValidator<LoginRequest> loginRequestValidator,
        IValidator<MfaVerifyRequest> mfaVerifyValidator)
    {
        _authService = authService;
        _loginRequestValidator = loginRequestValidator;
        _mfaVerifyValidator = mfaVerifyValidator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _loginRequestValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var result = await _authService.LoginAsync(request, cancellationToken);

        if (result.MfaRequired)
        {
            // Correct password, MFA on: no session yet. The client must call auth/mfa/verify with this challenge.
            return Ok(new MfaChallengeResponse(true, result.MfaChallengeToken!));
        }

        if (!result.Succeeded)
        {
            return Unauthorized();
        }

        SetRefreshCookie(result.Tokens!);

        return Ok(ToResponse(result));
    }

    /// <summary>The second sign-in step for an account with MFA on: the challenge from <c>login</c> plus an authenticator
    /// code or an unused backup code. Issues the same session as a normal sign-in.</summary>
    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequest request, CancellationToken cancellationToken)
    {
        var validation = await _mfaVerifyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var result = await _authService.VerifyMfaAsync(request.ChallengeToken, request.Code, cancellationToken);

        switch (result.Outcome)
        {
            case MfaVerifyOutcome.Success:
                SetRefreshCookie(result.Auth.Tokens!);
                return Ok(ToResponse(result.Auth));
            case MfaVerifyOutcome.LockedOut:
                return StatusCode(StatusCodes.Status429TooManyRequests, new { error = MfaService.LockedMessage });
            default:
                return BadRequest(new { error = "The code is incorrect, or the sign-in has expired. Try again or sign in again." });
        }
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
            user.Permissions,
            user.MfaSetupRequired));
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
            result.User.Permissions,
            result.User.MfaSetupRequired));
}
