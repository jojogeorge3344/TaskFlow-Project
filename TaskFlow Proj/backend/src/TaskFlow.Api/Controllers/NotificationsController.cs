using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Notifications;
using TaskFlow.Application.Tasks;

namespace TaskFlow.Api.Controllers;

/// <summary>The caller's own notifications for the Notification Center (Screen 12). Every query is
/// scoped to the authenticated user.</summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>A page of the caller's notifications, newest first, optionally filtered by type or read state.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List(
        [FromQuery] NotificationFilter filter,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId))
        {
            return Unauthorized();
        }

        var notifications = await _notificationService.ListAsync(userId, filter, cancellationToken);
        return Ok(notifications);
    }

    /// <summary>How many of the caller's notifications are still unread, for the header badge.</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId))
        {
            return Unauthorized();
        }

        var count = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new UnreadCountDto(count));
    }

    /// <summary>Marks one of the caller's notifications read; 404 when no owned row matches.</summary>
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId))
        {
            return Unauthorized();
        }

        var found = await _notificationService.MarkReadAsync(userId, id, cancellationToken);
        return found ? NoContent() : NotFound();
    }

    /// <summary>Marks every one of the caller's notifications read.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId))
        {
            return Unauthorized();
        }

        await _notificationService.MarkAllReadAsync(userId, cancellationToken);
        return NoContent();
    }

    private bool TryGetCurrentUser(out int userId)
    {
        userId = 0;

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(subject, out userId);
    }
}
