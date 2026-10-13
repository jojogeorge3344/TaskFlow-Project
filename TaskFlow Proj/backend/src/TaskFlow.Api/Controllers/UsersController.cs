using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Users;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>Read-only user list. Managers need it to pick assignees and recipients when
    /// creating or reassigning tasks; changes to users stay Administrator-only.</summary>
    [HttpGet]
    [Authorize(Policy = AuthPolicies.ManagerOrAdmin)]
    public async Task<ActionResult<IReadOnlyCollection<UserDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? departmentId,
        [FromQuery] Domain.Enums.Role? role,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var filter = new UserListFilter(search, departmentId, role, isActive);
        var users = await _userService.GetAllAsync(filter, cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(subject, out var currentUserId))
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole(nameof(Domain.Enums.Role.Administrator));
        if (!isAdmin && currentUserId != id)
        {
            return Forbid();
        }

        var user = await _userService.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<UserDto>> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userService.UpdateAsync(id, request, cancellationToken);
            if (user is null)
            {
                return NotFound();
            }
            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<UserDto>> UpdateStatus(int id, [FromBody] UserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateStatusAsync(id, request, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }
        return Ok(user);
    }
}
