using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.PasswordReset;
using TaskFlow.Application.PasswordReset.Validators;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Mail;

namespace TaskFlow.Api.Controllers;

/// <summary>The administrator's Password reset requests list and processing actions on the Employee
/// Management screen (Screen 7). Everything here requires the same permission that already guards
/// password changes; the responses never carry a password of any kind (design D4).</summary>
[ApiController]
[Route("api/v1/password-reset-requests")]
public class PasswordResetRequestsController : ControllerBase
{
    private readonly IPasswordResetService _passwordResetService;
    private readonly IValidator<PasswordResetProcessRequest> _processValidator;
    private readonly MailOptions _mailOptions;

    public PasswordResetRequestsController(
        IPasswordResetService passwordResetService,
        IValidator<PasswordResetProcessRequest> processValidator,
        IOptions<MailOptions> mailOptions)
    {
        _passwordResetService = passwordResetService;
        _processValidator = processValidator;
        _mailOptions = mailOptions.Value;
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.EmployeesManage)]
    public async Task<ActionResult<IReadOnlyCollection<PasswordResetRequestSummary>>> GetAll(
        CancellationToken cancellationToken)
    {
        var requests = await _passwordResetService.ListAsync(cancellationToken);
        return Ok(requests);
    }

    /// <summary>Processes one request: sets the new password (generated for Login-Screen origins), then
    /// notifies the user and only then completes the request (design D8). A refused or failed attempt
    /// says so honestly; it never reports a completion that did not happen.</summary>
    [HttpPost("{referenceId:guid}/process")]
    [RequirePermission(PermissionKeys.EmployeesManage)]
    public async Task<IActionResult> Process(
        Guid referenceId,
        [FromBody] PasswordResetProcessRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _processValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _passwordResetService.ProcessAsync(
            referenceId,
            request.Password,
            currentUserId,
            cancellationToken);

        return ToHttpResult(result);
    }

    /// <summary>The encrypted login link for the user of a Completed request, for the administrator to pass on with
    /// the password (design D14). It holds no password; only the account's latest completed reset can yield one.</summary>
    [HttpGet("{referenceId:guid}/login-link")]
    [RequirePermission(PermissionKeys.EmployeesManage)]
    public async Task<IActionResult> GetLoginLink(Guid referenceId, CancellationToken cancellationToken)
    {
        var result = await _passwordResetService.CreateLoginLinkAsync(referenceId, cancellationToken);
        switch (result.Outcome)
        {
            case PasswordResetLinkOutcome.Ok:
                var path = $"/login/{result.Token}";
                var baseUrl = _mailOptions.AppBaseUrl?.TrimEnd('/');
                return Ok(new { path, url = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl + path });
            case PasswordResetLinkOutcome.NotLatestCompleted:
                return Conflict(new { error = "A login link exists only for the account's latest completed reset, until the user has signed in or updated their password." });
            default:
                return NotFound(new { error = "No password recovery request carries that reference." });
        }
    }

    /// <summary>Re-runs only the user notification of a Failed request. No password is re-hashed and
    /// no temporary password is re-generated: those are gone by design (design D8).</summary>
    [HttpPost("{referenceId:guid}/retry-notification")]
    [RequirePermission(PermissionKeys.EmployeesManage)]
    public async Task<IActionResult> RetryNotification(Guid referenceId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _passwordResetService.RetryNotificationAsync(referenceId, currentUserId, cancellationToken);
        return ToHttpResult(result);
    }

    private IActionResult ToHttpResult(PasswordResetProcessResult result) => result.Outcome switch
    {
        PasswordResetProcessOutcome.Completed => Ok(new { status = PasswordResetStatus.Completed.ToString() }),
        // The password change landed but the user could not be told; the UI shows the failure reason
        // and offers the retry action. Honest 502, never a success.
        PasswordResetProcessOutcome.NotificationFailed => StatusCode(StatusCodes.Status502BadGateway, new { status = PasswordResetStatus.Failed.ToString(), error = result.Reason }),
        PasswordResetProcessOutcome.InvalidPassword => BadRequest(new { error = result.Reason }),
        PasswordResetProcessOutcome.AccountUnavailable => BadRequest(new { error = result.Reason }),
        PasswordResetProcessOutcome.AlreadyProcessed => Conflict(new { error = result.Reason }),
        _ => NotFound(new { error = result.Reason })
    };
}
