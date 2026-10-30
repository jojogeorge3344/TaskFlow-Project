using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Application.Tasks;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/v1/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ITaskCommentService _commentService;
    private readonly ITaskActivityService _activityService;
    private readonly IAuthorizationService _authorizationService;


    public TasksController(
        ITaskService taskService,
        ITaskCommentService commentService,
        ITaskActivityService activityService,
        IAuthorizationService authorizationService)
    {
        _taskService = taskService;
        _commentService = commentService;
        _activityService = activityService;
        _authorizationService = authorizationService;
    }

    /// <summary>A page of tasks the caller may access, with search, filters and paging (Screen 7).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskListItemDto>>> List(
        [FromQuery] TaskListFilter filter,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        var tasks = await _taskService.ListAsync(filter, userId, role, cancellationToken);
        return Ok(tasks);
    }

    /// <summary>Creates a task and assigns it to an employee (Screen 8). Manager/Administrator only.</summary>
    [HttpPost]
    [RequirePermission(PermissionKeys.TasksCreate)]
    public async Task<ActionResult<TaskDetailsDto>> Create(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        var task = await _taskService.CreateAsync(request, userId, role, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    /// <summary>The distinct assignees of tasks the caller may access, for the list's employee filter.</summary>
    [HttpGet("assignees")]
    public async Task<ActionResult<IReadOnlyCollection<TaskRecipientDto>>> GetAssignees(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        var assignees = await _taskService.GetAssigneesAsync(userId, role, cancellationToken);
        return Ok(assignees);
    }

    /// <summary>All tasks the caller may access, flat list the Kanban board groups client-side.</summary>
    [HttpGet("board")]
    [RequirePermission(PermissionKeys.BoardView)]
    public async Task<ActionResult<IReadOnlyCollection<TaskCardDto>>> GetBoard(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        var tasks = await _taskService.GetBoardAsync(userId, role, cancellationToken);
        return Ok(tasks);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskDetailsDto>> GetById(int id, CancellationToken cancellationToken)
    {
        if (!await CanAccessTaskAsync(id))
        {
            return Forbid();
        }

        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        var task = await _taskService.GetDetailsAsync(id, userId, role, cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpPut("{id:int}")]
    [RequirePermission(PermissionKeys.TasksUpdate)]
    public async Task<ActionResult<TaskDetailsDto>> Update(
        int id,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        var task = await _taskService.UpdateAsync(id, request, userId, role, cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }

    /// <summary>Adds worked hours to a task. The task's assignee can do this without edit rights.</summary>
    [HttpPost("{id:int}/hours")]
    public async Task<ActionResult<TaskDetailsDto>> LogHours(
        int id,
        [FromBody] LogHoursRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        var task = await _taskService.LogHoursAsync(id, request, userId, role, cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }

    /// <summary>Deleting a task requires the tasks.delete permission.</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionKeys.TasksDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out _))
        {
            return Unauthorized();
        }

        var deleted = await _taskService.DeleteAsync(id, userId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.TasksStatus)]
    public async Task<ActionResult<TaskCardDto>> ChangeStatus(
        int id,
        [FromBody] UpdateTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessTaskAsync(id))
        {
            return Forbid();
        }

        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        var task = await _taskService.ChangeStatusAsync(id, request, userId, role, cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpPost("{id:int}/reassign")]
    [RequirePermission(PermissionKeys.TasksReassign)]
    public async Task<ActionResult<TaskDetailsDto>> Reassign(
        int id,
        [FromBody] ReassignTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        var task = await _taskService.ReassignAsync(id, request, userId, role, cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpGet("{id:int}/recipients")]
    public async Task<ActionResult<IReadOnlyCollection<TaskRecipientDto>>> GetRecipients(int id, CancellationToken cancellationToken)
    {
        if (!await CanAccessTaskAsync(id))
        {
            return Forbid();
        }

        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        var recipients = await _taskService.GetRecipientsAsync(id, userId, role, cancellationToken);
        return recipients is null ? NotFound() : Ok(recipients);
    }

    [HttpPut("{id:int}/recipients")]
    [RequirePermission(PermissionKeys.TasksRecipients)]
    public async Task<IActionResult> SetRecipients(
        int id,
        [FromBody] SetRecipientsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        var updated = await _taskService.SetRecipientsAsync(id, request, userId, role, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpGet("{id:int}/comments")]
    public async Task<ActionResult<IReadOnlyCollection<TaskCommentDto>>> GetComments(int id, CancellationToken cancellationToken)
    {
        if (!await CanAccessTaskAsync(id))
        {
            return Forbid();
        }

        var comments = await _commentService.GetForTaskAsync(id, cancellationToken);
        return Ok(comments);
    }

    [HttpPost("{id:int}/comments")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.TasksCommentCreate)]
    public async Task<ActionResult<TaskCommentDto>> AddComment(
        int id,
        [FromBody] AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessTaskAsync(id))
        {
            return Forbid();
        }

        if (!TryGetCurrentUser(out var userId, out _))
        {
            return Unauthorized();
        }

        var comment = await _commentService.AddAsync(id, request, userId, cancellationToken);
        return Ok(comment);
    }

    [HttpPut("{id:int}/comments/{commentId:int}")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.TasksCommentEdit)]
    public async Task<ActionResult<TaskCommentDto>> UpdateComment(
        int id,
        int commentId,
        [FromBody] AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessTaskAsync(id))
        {
            return Forbid();
        }

        if (!TryGetCurrentUser(out var userId, out _))
        {
            return Unauthorized();
        }

        var comment = await _commentService.UpdateAsync(id, commentId, request, userId, cancellationToken);
        return comment is null ? NotFound() : Ok(comment);
    }

    [HttpDelete("{id:int}/comments/{commentId:int}")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.TasksCommentDelete)]
    public async Task<IActionResult> DeleteComment(int id, int commentId, CancellationToken cancellationToken)
    {
        if (!await CanAccessTaskAsync(id))
        {
            return Forbid();
        }

        if (!TryGetCurrentUser(out var userId, out _))
        {
            return Unauthorized();
        }

        return await _commentService.DeleteAsync(id, commentId, userId, cancellationToken) ? NoContent() : NotFound();
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