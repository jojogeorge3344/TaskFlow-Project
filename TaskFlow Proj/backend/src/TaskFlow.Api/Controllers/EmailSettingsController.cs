using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Email;
using TaskFlow.Application.Settings;

namespace TaskFlow.Api.Controllers;

/// <summary>Email delivery configuration, test email and delivery log (Screen 14). Administrator only. The
/// SMTP password is never accepted or returned here; it lives in server configuration.</summary>
[ApiController]
[Route("api/v1/settings/email")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class EmailSettingsController : ControllerBase
{
    private readonly IEmailSettingsService _settingsService;
    private readonly IEmailDeliveryService _deliveryService;
    private readonly IValidator<UpdateEmailSettingsRequest> _settingsValidator;
    private readonly IValidator<SendTestEmailRequest> _testValidator;

    public EmailSettingsController(
        IEmailSettingsService settingsService,
        IEmailDeliveryService deliveryService,
        IValidator<UpdateEmailSettingsRequest> settingsValidator,
        IValidator<SendTestEmailRequest> testValidator)
    {
        _settingsService = settingsService;
        _deliveryService = deliveryService;
        _settingsValidator = settingsValidator;
        _testValidator = testValidator;
    }

    /// <summary>The current email configuration, the password indicator and any configuration problems.</summary>
    [HttpGet]
    public async Task<ActionResult<EmailSettingsDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _settingsService.GetAsync(cancellationToken));
    }

    /// <summary>Saves the email configuration, including the email switch shared with Reminder Settings.</summary>
    [HttpPut]
    public async Task<ActionResult<EmailSettingsDto>> Update(
        [FromBody] UpdateEmailSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _settingsValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _settingsService.UpdateAsync(userId, request, cancellationToken));
    }

    /// <summary>Sends a test email with the saved configuration. A failed send is a normal result (200 with
    /// success false), not a server error.</summary>
    [HttpPost("test")]
    public async Task<ActionResult<TestEmailResultDto>> SendTest(
        [FromBody] SendTestEmailRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _testValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _deliveryService.SendTestAsync(userId, request.ToAddress, cancellationToken);
        if (result is null)
        {
            ModelState.AddModelError(nameof(SendTestEmailRequest.ToAddress), "Your account has no email address. Enter an address to send the test to.");
            return ValidationProblem(ModelState);
        }

        return Ok(result);
    }

    /// <summary>The most recent email deliveries, newest first.</summary>
    [HttpGet("deliveries")]
    public async Task<ActionResult<IReadOnlyList<EmailDeliveryDto>>> GetDeliveries(
        [FromQuery] int take = IEmailDeliveryService.DefaultRecentCount,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _deliveryService.GetRecentAsync(take, cancellationToken));
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
