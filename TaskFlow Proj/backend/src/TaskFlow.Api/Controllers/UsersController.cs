using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Authentication;
using TaskFlow.Application.Users;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<UpdateOwnProfileRequest> _profileValidator;
    private readonly IValidator<ChangePasswordRequest> _passwordValidator;

    public UsersController(
        IUserService userService,
        IValidator<UpdateOwnProfileRequest> profileValidator,
        IValidator<ChangePasswordRequest> passwordValidator)
    {
        _userService = userService;
        _profileValidator = profileValidator;
        _passwordValidator = passwordValidator;
    }

    /// <summary>The caller's own account details (Screen 19). The id always comes from the token,
    /// so a user can never read or change somebody else's account here.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetOwn(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var user = await _userService.GetOwnAsync(currentUserId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Updates the caller's own full name and email. Role, department, username, employee
    /// code and account status are not part of this contract and cannot change here.</summary>
    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateOwn([FromBody] UpdateOwnProfileRequest request, CancellationToken cancellationToken)
    {
        var validation = await _profileValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        try
        {
            var user = await _userService.UpdateOwnProfileAsync(currentUserId, request, cancellationToken);
            return user is null ? NotFound() : Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Changes the caller's own password after verifying the current one. Every session of
    /// the user is ended, so the client must sign in again with the new password.</summary>
    [HttpPost("me/password")]
    [Authorize]
    public async Task<ActionResult> ChangeOwnPassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await _passwordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        try
        {
            var user = await _userService.ChangeOwnPasswordAsync(currentUserId, request, cancellationToken);
            return user is null ? NotFound() : NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Read-only user list. Managers need it to pick assignees and recipients when
    /// creating or reassigning tasks; changes to users stay Administrator-only.</summary>
    [HttpGet]
    [RequirePermission(PermissionKeys.EmployeesView)]
    public async Task<ActionResult<IReadOnlyCollection<UserDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? departmentId,
        [FromQuery] string? role,
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

        var isAdmin = User.HasClaim(AuthClaims.Permission, PermissionKeys.EmployeesManage);
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
    [RequirePermission(PermissionKeys.EmployeesManage)]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        // Choosing any role other than Employee is role management, which only Administrators may do.
        if (request.RoleId != SystemRoles.EmployeeId && !CanManageRoles())
        {
            return Forbid();
        }

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
    [RequirePermission(PermissionKeys.EmployeesManage)]
    public async Task<ActionResult<UserDto>> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        // Changing a user's role is role management, which only Administrators may do.
        if (!CanManageRoles())
        {
            var current = await _userService.GetByIdAsync(id, cancellationToken);
            if (current is not null && current.RoleId != request.RoleId)
            {
                return Forbid();
            }
        }

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
    [RequirePermission(PermissionKeys.EmployeesManage)]
    public async Task<ActionResult<UserDto>> UpdateStatus(int id, [FromBody] UserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateStatusAsync(id, request, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    private bool CanManageRoles() => User.HasClaim(AuthClaims.Permission, PermissionKeys.RolesManage);
}
