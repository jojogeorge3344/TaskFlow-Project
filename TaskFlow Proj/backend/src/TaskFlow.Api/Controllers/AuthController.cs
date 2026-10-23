using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.Cookies;
using TaskFlow.Application.Auth;
using TaskFlow.Application.Mfa;
using TaskFlow.Application.Tasks;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using MailFolder = TaskFlow.Domain.Enums.MailFolder;
using MailMessage = TaskFlow.Domain.Entities.MailMessage;
using MailRecipient = TaskFlow.Domain.Entities.MailRecipient;
using MailboxItem = TaskFlow.Domain.Entities.MailboxItem;
using User = TaskFlow.Domain.Entities.User;
using UserDetailsDto = TaskFlow.Application.Users.UserDto;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<LoginRequest> _loginRequestValidator;
    private readonly IValidator<MfaVerifyRequest> _mfaVerifyValidator;
    private readonly IPasswordResetTokenService _passwordResetTokenService;
    private readonly IPasswordResetRateLimiter _passwordResetRateLimiter;
    private readonly IPasswordResetService _passwordResetService;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly INotificationPublisher _notificationPublisher;

    public AuthController(
        IAuthService authService,
        IValidator<LoginRequest> loginRequestValidator,
        IValidator<MfaVerifyRequest> mfaVerifyValidator,
        IPasswordResetTokenService passwordResetTokenService,
        IPasswordResetRateLimiter passwordResetRateLimiter,
        IPasswordResetService passwordResetService,
        AppDbContext dbContext,
        IConfiguration configuration,
        INotificationPublisher notificationPublisher)
    {
        _authService = authService;
        _loginRequestValidator = loginRequestValidator;
        _mfaVerifyValidator = mfaVerifyValidator;
        _passwordResetTokenService = passwordResetTokenService;
        _passwordResetRateLimiter = passwordResetRateLimiter;
        _passwordResetService = passwordResetService;
        _dbContext = dbContext;
        _configuration = configuration;
        _notificationPublisher = notificationPublisher;
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

    /// <summary>Requests a password reset for the signed-in user (the Profile screen). The account always comes from
    /// the caller's token, never from the request. A one-time, time-limited reset link is emailed to an Administrator;
    /// when the caller is an Administrator the system generates the new password directly, with no
    /// administrator-email step. Signed-out users recover with their authenticator app instead.</summary>
    [HttpPost("forgot-password")]
    [Authorize]
    public async Task<IActionResult> ForgotPassword(CancellationToken cancellationToken)
    {
        const string genericMessage = "If an account matches, a password reset has been initiated.";

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var user = await _dbContext.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == currentUserId && !u.IsDeleted, cancellationToken);

        if (user is not null)
        {
            var administrator = await FindAdministratorAsync(cancellationToken);

            // Throttle per requesting account and per Administrator recipient. A throttled request looks
            // exactly like a successful one, so it cannot be used to probe which accounts exist.
            if (!_passwordResetRateLimiter.TryAcquire(user.Id, administrator?.Id))
            {
                return Ok(new { message = genericMessage });
            }

            // Administrators trigger the auto-generate path directly from the login page.
            if (user.RoleId == SystemRoles.AdministratorId)
            {
                var generatedPassword = await _passwordResetService.ApplyGeneratedPasswordAsync(
                    user,
                    token: null,
                    "Your password was regenerated by the system. Please sign in with your new password.",
                    cancellationToken);

                await SendNewPasswordMailAsync(user, generatedPassword, cancellationToken);

                return Ok(new { message = genericMessage });
            }

            // Generate a one-time reset token valid for 5 minutes and email the link to an Administrator.
            var token = await _passwordResetTokenService.RequestResetTokenAsync(user.Id, cancellationToken);
            await SendResetRequestMailAsync(user, token, administrator, cancellationToken);
        }

        return Ok(new { message = genericMessage });
    }

    /// <summary>Validates a one-time reset token and returns the user details for the Administrator.
    /// Used when the Administrator opens the reset link from the email.</summary>
    [HttpGet("reset-password/{token}")]
    [RequirePermission(PermissionKeys.EmployeesManage)]
    public async Task<ActionResult<PasswordResetRequestDetailsDto>> ValidateResetToken(
        string token,
        CancellationToken cancellationToken)
    {
        var resetToken = await _passwordResetTokenService.FindValidAsync(token, cancellationToken);

        if (resetToken?.User is null)
        {
            return BadRequest(new { error = "Link not valid or expired." });
        }

        var userDto = await _dbContext.Users
            .Include(u => u.Role)
            .Where(u => u.Id == resetToken.UserId && !u.IsDeleted)
            .Select(u => new UserDetailsDto(
                u.Id,
                u.EmployeeCode,
                u.FullName,
                u.Username,
                u.Email,
                u.RoleId,
                u.Role.Name,
                u.IsActive,
                u.CreatedAt,
                u.UpdatedAt,
                u.LastLoginAt,
                u.ProjectId,
                u.Project != null ? u.Project.Name : null,
                u.DepartmentId,
                u.Department != null ? u.Department.Name : null,
                u.ManagerId,
                u.Manager != null ? u.Manager.FullName : null,
                u.ManagerProjects
                    .OrderBy(link => link.Project.Name)
                    .Select(link => new ManagerProjectDto(link.ProjectId, link.Project.Name))
                    .ToList(),
                u.Reports
                    .OrderBy(report => report.FullName)
                    .Select(report => new ManagerReportDto(report.Id, report.FullName))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        if (userDto is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        var requests = await _dbContext.PasswordResetTokens
            .AsNoTracking()
            .Where(stored => stored.UserId == resetToken.UserId)
            .OrderByDescending(stored => stored.CreatedAt)
            .ThenByDescending(stored => stored.Id)
            .Select(stored => new { stored.Id, stored.CreatedAt, stored.ExpiresAt, stored.Used })
            .ToListAsync(cancellationToken);

        var history = requests
            .Select(stored => new PasswordResetRequestEntryDto(
                stored.Id,
                stored.CreatedAt,
                stored.ExpiresAt,
                stored.Used ? "Used" : stored.ExpiresAt <= now ? "Expired" : "Pending"))
            .ToList();

        return Ok(new PasswordResetRequestDetailsDto(
            userDto,
            history.Count,
            history.Count > 0 ? history[0].RequestedAt : null,
            resetToken.ExpiresAt,
            history));
    }

    /// <summary>An Administrator generates a new password for the user from the user details screen.
    /// Consumes this exact one-time token, ends all sessions, and sends the new password by email plus an in-app
    /// notification. The generated password is never returned in the response.</summary>
    [HttpPost("reset-password")]
    [RequirePermission(PermissionKeys.EmployeesManage)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var resetToken = await _passwordResetTokenService.FindValidAsync(request.Token, cancellationToken);

        if (resetToken?.User is null)
        {
            return BadRequest(new { error = "Link not valid or expired." });
        }

        var generatedPassword = await _passwordResetService.ApplyGeneratedPasswordAsync(
            resetToken.User,
            resetToken,
            "Your password was regenerated by an Administrator. Please sign in with your new password.",
            cancellationToken);

        // Send the new password to the user by email; the notification was published by the reset service.
        await SendNewPasswordMailAsync(resetToken.User, generatedPassword, cancellationToken);

        return NoContent();
    }

    /// <summary>Sends a system mail to an Administrator with a one-time reset link.</summary>
    private async Task SendResetRequestMailAsync(
        User user,
        string resetToken,
        User? administrator,
        CancellationToken cancellationToken)
    {
        if (administrator is null)
        {
            return;
        }

        // The link opens the Angular app (Mail:AppBaseUrl), not the API.
        var baseUrl = (_configuration["Mail:AppBaseUrl"] ?? "http://localhost:4200").TrimEnd('/');
        var resetLink = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}&userId={user.Id}";

        var now = DateTime.UtcNow;
        var message = new MailMessage
        {
            ConversationId = Guid.NewGuid(),
            IsSystem = true,
            IsPasswordRequest = true,
            Subject = "TaskFlow - Password reset requested",
            Body = $@"{user.FullName} ({user.Email}) has requested a password reset.

To generate a new password for this user, open the reset link below:
{resetLink}

The link is valid for 5 minutes and can be used only once.",
            CreatedAt = now,
            UpdatedAt = now,
            SentAt = now
        };

        _dbContext.MailMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.MailRecipients.Add(new MailRecipient
        {
            MessageId = message.Id,
            UserId = administrator.Id,
            Kind = MailRecipientKind.To
        });

        _dbContext.MailboxItems.Add(new MailboxItem
        {
            MessageId = message.Id,
            OwnerUserId = administrator.Id,
            Folder = MailFolder.Inbox,
            IsRead = false,
            SortAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationPublisher.PublishAsync(
            administrator.Id,
            taskId: null,
            NotificationType.System,
            $"{user.FullName} requested a password reset. Open the Password Request folder in Mail.",
            cancellationToken);
    }

    /// <summary>Sends a system mail to the user with their new password.</summary>
    private async Task SendNewPasswordMailAsync(User user, string newPassword, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var message = new MailMessage
        {
            ConversationId = Guid.NewGuid(),
            IsSystem = true,
            Subject = "TaskFlow - Your password has been reset",
            Body = $@"{user.FullName},

Your password has been reset.

Your new password is: {newPassword}

Please sign in with this password and change it at your earliest convenience.",
            CreatedAt = now,
            UpdatedAt = now,
            SentAt = now
        };

        _dbContext.MailMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.MailRecipients.Add(new MailRecipient
        {
            MessageId = message.Id,
            UserId = user.Id,
            Kind = MailRecipientKind.To
        });

        _dbContext.MailboxItems.Add(new MailboxItem
        {
            MessageId = message.Id,
            OwnerUserId = user.Id,
            Folder = MailFolder.Inbox,
            IsRead = false,
            SortAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Finds the Administrator who receives reset requests.</summary>
    private async Task<User?> FindAdministratorAsync(CancellationToken cancellationToken) =>
        await _dbContext.Users
            .AsNoTracking()
            .Where(candidate =>
                candidate.RoleId == SystemRoles.AdministratorId &&
                candidate.IsActive &&
                !candidate.IsDeleted)
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);

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
