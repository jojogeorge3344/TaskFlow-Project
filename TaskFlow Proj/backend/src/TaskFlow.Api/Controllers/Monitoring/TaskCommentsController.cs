using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Monitoring.TaskManagement.Discussion;
using TaskFlow.Api.Services.Monitoring.TaskManagement.Discussion;
using TaskFlow.Api.Authorization.Policies;

namespace TaskFlow.Api.Controllers.Monitoring;

/// <summary>A task's discussion. Every endpoint first checks the caller may see the task.</summary>
[ApiController]
[Route("api/v1/tasks")]
[Authorize]
public class TaskCommentsController : ControllerBase
{
    private readonly ITaskCommentService _commentService;
    private readonly IAuthorizationService _authorizationService;

    public TaskCommentsController(ITaskCommentService commentService, IAuthorizationService authorizationService)
    {
        _commentService = commentService;
        _authorizationService = authorizationService;
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
