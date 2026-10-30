using TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings;
using TaskFlow.Api.Dtos.Settings.ApplicationSettings;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.Api.Controllers.Settings.ApplicationSettings;

/// <summary>Application-level settings (Screen 20). Any signed-in user may read the non-secret
/// values (the brand name and default priority are used across screens); only an Administrator may
/// change them.</summary>
[ApiController]
[Route("api/v1/settings/application")]
public class ApplicationSettingsController : ControllerBase
{
    private readonly IApplicationSettingsService _applicationSettingsService;

    public ApplicationSettingsController(
        IApplicationSettingsService applicationSettingsService)
    {
        _applicationSettingsService = applicationSettingsService;
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
    [RequirePermission(PermissionKeys.SettingsApplication)]
    public async Task<ActionResult<ApplicationSettingsDto>> Update(
        [FromBody] UpdateApplicationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _applicationSettingsService.UpdateAsync(userId, request, cancellationToken));
    }
}
