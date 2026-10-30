using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;
using TaskFlow.Application.Settings;

namespace TaskFlow.Api.Controllers;

/// <summary>Global reminder settings (Screen 13). Needs settings.reminders.manage.</summary>
[ApiController]
[Route("api/v1/settings")]
[RequirePermission(PermissionKeys.SettingsReminders)]
public class SettingsController : ControllerBase
{
    private readonly IReminderSettingsService _reminderSettingsService;

    public SettingsController(
        IReminderSettingsService reminderSettingsService)
    {
        _reminderSettingsService = reminderSettingsService;
    }

    /// <summary>The current reminder configuration, including the read-only application timezone.</summary>
    [HttpGet("reminders")]
    public async Task<ActionResult<ReminderSettingsDto>> GetReminders(CancellationToken cancellationToken)
    {
        return Ok(await _reminderSettingsService.GetAsync(cancellationToken));
    }

    /// <summary>Saves the reminder configuration. The timezone is not part of the request and never changes here.</summary>
    [HttpPut("reminders")]
    public async Task<ActionResult<ReminderSettingsDto>> UpdateReminders(
        [FromBody] UpdateReminderSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _reminderSettingsService.UpdateAsync(userId, request, cancellationToken));
    }
}
