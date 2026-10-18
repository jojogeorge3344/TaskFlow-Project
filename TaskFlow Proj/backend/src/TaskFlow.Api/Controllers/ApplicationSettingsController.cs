using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Settings;

namespace TaskFlow.Api.Controllers;

/// <summary>Application-level settings (Screen 20). Any signed-in user may read the non-secret
/// values (the brand name and default priority are used across screens); only an Administrator may
/// change them.</summary>
[ApiController]
[Route("api/v1/settings/application")]
public class ApplicationSettingsController : ControllerBase
{
    private readonly IApplicationSettingsService _applicationSettingsService;
    private readonly IValidator<UpdateApplicationSettingsRequest> _validator;

    public ApplicationSettingsController(
        IApplicationSettingsService applicationSettingsService,
        IValidator<UpdateApplicationSettingsRequest> validator)
    {
        _applicationSettingsService = applicationSettingsService;
        _validator = validator;
    }

    /// <summary>The current application configuration. Non-secret, readable by every signed-in user.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApplicationSettingsDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _applicationSettingsService.GetAsync(cancellationToken));
    }

    /// <summary>Saves the application configuration. Administrator only.</summary>
    [HttpPut]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<ApplicationSettingsDto>> Update(
        [FromBody] UpdateApplicationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _applicationSettingsService.UpdateAsync(userId, request, cancellationToken));
    }
}
