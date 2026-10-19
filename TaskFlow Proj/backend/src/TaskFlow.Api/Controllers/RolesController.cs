using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Roles;
using TaskFlow.Domain.Security;

namespace TaskFlow.Api.Controllers;

/// <summary>Roles and the permission catalog (Permissions Management). Administrator only: the roles.manage permission
/// is held by the Administrator system role and cannot be given to another role.</summary>
[ApiController]
[Route("api/v1")]
[RequirePermission(PermissionKeys.RolesManage)]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roles;

    public RolesController(IRoleService roles)
    {
        _roles = roles;
    }

    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles(CancellationToken cancellationToken) =>
        Ok(await _roles.GetRolesAsync(cancellationToken));

    [HttpGet("roles/{id:int}")]
    public Task<ActionResult<RoleDto>> GetRole(int id, CancellationToken cancellationToken) =>
        RunAsync(() => _roles.GetRoleAsync(id, cancellationToken));

    [HttpPost("roles")]
    public async Task<ActionResult<RoleDto>> Create([FromBody] SaveRoleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _roles.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetRole), new { id = created.Id }, created);
        }
        catch (RoleException exception)
        {
            return ToProblem(exception);
        }
    }

    [HttpPut("roles/{id:int}")]
    public Task<ActionResult<RoleDto>> Update(int id, [FromBody] SaveRoleRequest request, CancellationToken cancellationToken) =>
        RunAsync(() => _roles.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("roles/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _roles.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (RoleException exception)
        {
            return ToProblem(exception);
        }
    }

    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> GetPermissions(CancellationToken cancellationToken) =>
        Ok(await _roles.GetPermissionsAsync(cancellationToken));

    // The catalog is fixed: nobody, including an Administrator, can add, change or remove a permission.
    [HttpPost("permissions")]
    [HttpPut("permissions/{id:int}")]
    [HttpDelete("permissions/{id:int}")]
    public IActionResult CatalogIsFixed() =>
        StatusCode(StatusCodes.Status403Forbidden, new { error = "The permission list is fixed and cannot be changed." });

    private async Task<ActionResult<RoleDto>> RunAsync(Func<Task<RoleDto>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (RoleException exception)
        {
            return ToProblem(exception);
        }
    }

    private ObjectResult ToProblem(RoleException exception) => exception.Failure switch
    {
        RoleFailure.NotFound => NotFound(new { error = exception.Message }),
        RoleFailure.Invalid => BadRequest(new { error = exception.Message }),
        RoleFailure.ReadOnly => StatusCode(StatusCodes.Status403Forbidden, new { error = exception.Message }),
        _ => Conflict(new { error = exception.Message })
    };
}
