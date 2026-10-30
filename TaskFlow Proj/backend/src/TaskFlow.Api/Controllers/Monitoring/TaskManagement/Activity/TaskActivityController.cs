using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Monitoring.TaskManagement.Activity;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Services.Interfaces.Monitoring.TaskManagement.Activity;

namespace TaskFlow.Api.Controllers.Monitoring.TaskManagement.Activity;

/// <summary>A task's activity history, and the administrator's corrections to worked-hours entries.</summary>
[ApiController]
[Route("api/v1/tasks")]
[Authorize]
public class TaskActivityController : ControllerBase
{
    private readonly ITaskActivityService _activityService;
    private readonly IAuthorizationService _authorizationService;

    public TaskActivityController(ITaskActivityService activityService, IAuthorizationService authorizationService)
    {
        _activityService = activityService;
        _authorizationService = authorizationService;
    }

    /// <summary>Administrator only: correct the hours added by one "worked hours logged" history entry.</summary>
    [HttpPut("{id:int}/activity/{entryId:int}/hours")]
    public async Task<IActionResult> UpdateHoursEntry(
        int id,
        int entryId,
        [FromBody] UpdateHoursEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        if (role != SystemRoles.Administrator)
        {
            return Forbid();
        }

        return await _activityService.UpdateHoursEntryAsync(id, entryId, request.Hours, userId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>Administrator only: remove one "worked hours logged" history entry and rebuild the worked total.</summary>
    [HttpDelete("{id:int}/activity/{entryId:int}/hours")]
    public async Task<IActionResult> DeleteHoursEntry(int id, int entryId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        if (role != SystemRoles.Administrator)
        {
            return Forbid();
        }

        return await _activityService.DeleteHoursEntryAsync(id, entryId, userId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpGet("{id:int}/activity")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.TasksActivityView)]
    public async Task<ActionResult<IReadOnlyCollection<ActivityEntryDto>>> GetActivity(
        int id,
        [FromQuery] TaskActivityEventType? eventType,
        [FromQuery] int? actorUserId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessTaskAsync(id))
        {
            return Forbid();
        }

        DateTime? fromUtc = from?.Kind == DateTimeKind.Utc ? from : from?.ToUniversalTime();
        DateTime? toUtc = to;
        if (toUtc.HasValue)
        {
            var toValue = toUtc.Value;
            if (toValue.Kind != DateTimeKind.Utc)
            {
                toValue = toValue.ToUniversalTime();
            }
            // If the client passed a date-only value, include the whole day
            if (toValue.TimeOfDay == TimeSpan.Zero)
            {
                toValue = toValue.Date.AddDays(1).AddTicks(-1);
            }
            toUtc = toValue;
        }

        var filter = new ActivityFilter(eventType, actorUserId, fromUtc, toUtc, search);
        var entries = await _activityService.GetForTaskAsync(id, filter, cancellationToken);
        return Ok(entries);
    }

    private async Task<bool> CanAccessTaskAsync(int taskId)
    {
        var result = await _authorizationService.AuthorizeAsync(User, taskId, AuthPolicies.TaskAccess);
        return result.Succeeded;
    }

    private bool TryGetCurrentUser(out int userId, out string role)
    {
        userId = 0;
        role = string.Empty;

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(subject, out userId))
        {
            return false;
        }

        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return true;
    }
}
