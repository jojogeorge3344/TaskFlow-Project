using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Authentication;
using TaskFlow.Application.Mfa;
using TaskFlow.Application.Roles;
using TaskFlow.Application.Users;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<UpdateOwnProfileRequest> _profileValidator;
    private readonly IValidator<ChangePasswordRequest> _passwordValidator;
    private readonly IMfaService _mfaService;
    private readonly IRoleService _roleService;

    public UsersController(
        IUserService userService,
        IValidator<UpdateOwnProfileRequest> profileValidator,
        IValidator<ChangePasswordRequest> passwordValidator,
        IMfaService mfaService,
        IRoleService roleService)
    {
        _userService = userService;
        _profileValidator = profileValidator;
        _passwordValidator = passwordValidator;
        _mfaService = mfaService;
        _roleService = roleService;
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

    /// <summary>Updates the caller's own full name and email. Role, project, username, employee
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

    /// <summary>Changes the caller's own password after checking an authenticator code. Every session of
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
            // The authenticator code is the proof of identity. RequireCodeAsync passes silently when MFA is off, which the setup
            // gate prevents; refuse here too so a gate regression cannot leave this endpoint unprotected.
            var current = await _userService.GetOwnAsync(currentUserId, cancellationToken);
            if (current is null || !current.MfaEnabled)
            {
                return BadRequest(new { error = "Set up two-step verification before changing your password." });
            }

            await _mfaService.RequireCodeAsync(currentUserId, request.MfaCode, cancellationToken);

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
        [FromQuery] int? projectId,
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] int? departmentId,
        CancellationToken cancellationToken)
    {
        var filter = new UserListFilter(search, projectId, role, isActive, departmentId);
        var users = await _userService.GetAllAsync(filter, cancellationToken);
        return Ok(users);
    }

    /// <summary>The roles the employee form and role filter offer. Needs the assign roles permission, or role
    /// management; the Administrator role is listed only for callers who may hand it out.</summary>
    [HttpGet("roles")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<RoleOptionDto>>> GetRoleOptions(CancellationToken cancellationToken)
    {
        var canAssignAdministrator = CanAssignAdministrator();
        if (!CanManageRoles() && !User.HasClaim(AuthClaims.Permission, PermissionKeys.RolesManage))
        {
            return Forbid();
        }

        var roles = await _roleService.GetRolesAsync(cancellationToken);
        return Ok(roles
            .Where(role => canAssignAdministrator || role.Id != SystemRoles.AdministratorId)
            .Select(role => new RoleOptionDto(role.Id, role.Name, role.IsSystem))
            .ToList());
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

        var isAdmin = User.HasClaim(AuthClaims.Permission, PermissionKeys.EmployeesEdit);
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
    [RequirePermission(PermissionKeys.EmployeesCreate)]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        // Choosing any role other than Employee is role assignment, which needs its own permission.
        if (request.RoleId != SystemRoles.EmployeeId && !CanManageRoles())
        {
            return Forbid();
        }

        // The Administrator role is the highest privilege; handing it out needs its own permission, which only the Administrator role holds.
        if (request.RoleId == SystemRoles.AdministratorId && !CanAssignAdministrator())
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
    [RequirePermission(PermissionKeys.EmployeesEdit)]
    public async Task<ActionResult<UserDto>> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        // Changing a user's role needs the assign roles permission, and moving anyone into or out of the Administrator
        // role needs the Administrator-only permission as well.
        var current = await _userService.GetByIdAsync(id, cancellationToken);
        if (current is not null && current.RoleId != request.RoleId)
        {
            var involvesAdministrator = current.RoleId == SystemRoles.AdministratorId || request.RoleId == SystemRoles.AdministratorId;
            if (!CanManageRoles() || (involvesAdministrator && !CanAssignAdministrator()))
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
    [RequirePermission(PermissionKeys.EmployeesStatus)]
    public async Task<ActionResult<UserDto>> UpdateStatus(int id, [FromBody] UserStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userService.UpdateStatusAsync(id, request, cancellationToken);
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

    /// <summary>Deleting an employee requires the employees.delete permission.</summary>
    [HttpDelete("{id}")]
    [RequirePermission(PermissionKeys.EmployeesDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        try
        {
            var deleted = await _userService.DeleteAsync(id, currentUserId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool CanManageRoles() => User.HasClaim(AuthClaims.Permission, PermissionKeys.EmployeesRoleAssign);

    private bool CanAssignAdministrator() => User.HasClaim(AuthClaims.Permission, PermissionKeys.RolesAdministratorAssign);
}
