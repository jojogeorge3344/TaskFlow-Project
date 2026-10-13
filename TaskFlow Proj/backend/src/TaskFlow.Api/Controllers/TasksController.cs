using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Enums;

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

    private readonly IValidator<UpdateTaskRequest> _updateValidator;
    private readonly IValidator<UpdateTaskStatusRequest> _statusValidator;
    private readonly IValidator<ReassignTaskRequest> _reassignValidator;
    private readonly IValidator<AddCommentRequest> _commentValidator;
    private readonly IValidator<SetRecipientsRequest> _recipientsValidator;
    private readonly IValidator<CreateTaskRequest> _createValidator;

    public TasksController(
        ITaskService taskService,
        ITaskCommentService commentService,
        ITaskActivityService activityService,
        IAuthorizationService authorizationService,
        IValidator<UpdateTaskRequest> updateValidator,
        IValidator<UpdateTaskStatusRequest> statusValidator,
        IValidator<ReassignTaskRequest> reassignValidator,
        IValidator<AddCommentRequest> commentValidator,
        IValidator<SetRecipientsRequest> recipientsValidator,
        IValidator<CreateTaskRequest> createValidator)
    {
        _taskService = taskService;
        _commentService = commentService;
        _activityService = activityService;
        _authorizationService = authorizationService;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
        _reassignValidator = reassignValidator;
        _commentValidator = commentValidator;
        _recipientsValidator = recipientsValidator;
        _createValidator = createValidator;
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
    [Authorize(Policy = AuthPolicies.ManagerOrAdmin)]
    public async Task<ActionResult<TaskDetailsDto>> Create(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var task = await _taskService.CreateAsync(request, userId, role, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
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
    [Authorize(Policy = AuthPolicies.ManagerOrAdmin)]
    public async Task<ActionResult<TaskDetailsDto>> Update(
        int id,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var task = await _taskService.UpdateAsync(id, request, userId, role, cancellationToken);
            return task is null ? NotFound() : Ok(task);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<TaskCardDto>> ChangeStatus(
        int id,
        [FromBody] UpdateTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _statusValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!await CanAccessTaskAsync(id))
        {
            return Forbid();
        }

        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var task = await _taskService.ChangeStatusAsync(id, request, userId, role, cancellationToken);
            return task is null ? NotFound() : Ok(task);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{id:int}/reassign")]
    [Authorize(Policy = AuthPolicies.ManagerOrAdmin)]
    public async Task<ActionResult<TaskDetailsDto>> Reassign(
        int id,
        [FromBody] ReassignTaskRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _reassignValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var task = await _taskService.ReassignAsync(id, request, userId, role, cancellationToken);
            return task is null ? NotFound() : Ok(task);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
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
    [Authorize(Policy = AuthPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> SetRecipients(
        int id,
        [FromBody] SetRecipientsRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _recipientsValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!TryGetCurrentUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var updated = await _taskService.SetRecipientsAsync(id, request, userId, role, cancellationToken);
            return updated ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
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
    public async Task<ActionResult<TaskCommentDto>> AddComment(
        int id,
        [FromBody] AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _commentValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!await CanAccessTaskAsync(id))
        {
            return Forbid();
        }

        if (!TryGetCurrentUser(out var userId, out _))
        {
            return Unauthorized();
        }

        try
        {
            var comment = await _commentService.AddAsync(id, request, userId, cancellationToken);
            return Ok(comment);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{id:int}/activity")]
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

        var filter = new ActivityFilter(eventType, actorUserId, from, to, search);
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