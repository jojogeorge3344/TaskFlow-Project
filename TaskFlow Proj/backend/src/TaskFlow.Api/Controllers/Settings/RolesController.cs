using TaskFlow.Api.Services.Settings.RoleConfiguration;
using TaskFlow.Api.Dtos.Settings.RoleConfiguration;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Authorization.Policies;

namespace TaskFlow.Api.Controllers.Settings;

/// <summary>Roles and the permission catalog (Permissions Management). Open to any role holding roles.manage, which an
/// administrator can give to a custom role. System roles stay read-only and "Give the Administrator role" is not assignable.</summary>
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
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        if (!User.HasClaim("permission", PermissionKeys.RolesViewAll))
        {
            var all = await _roles.GetRolesAsync(cancellationToken);
            var roleName = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return Ok(Array.Empty<RoleDto>());
            }
            return Ok(all.Where(r => string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase)).ToList());
        }
        return Ok(await _roles.GetRolesAsync(cancellationToken));
    }

    [HttpGet("roles/{id:int}")]
    public Task<ActionResult<RoleDto>> GetRole(int id, CancellationToken cancellationToken) =>
        RunAsync(() => _roles.GetRoleAsync(id, cancellationToken));

    [HttpPost("roles")]
    public async Task<ActionResult<RoleDto>> Create([FromBody] SaveRoleRequest request, CancellationToken cancellationToken)
    {
        if (!User.HasClaim("permission", PermissionKeys.RolesModifyAccess))
        {
            return Forbid();
        }
        var created = await _roles.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetRole), new { id = created.Id }, created);
    }

    [HttpPut("roles/{id:int}")]
    public async Task<ActionResult<RoleDto>> Update(int id, [FromBody] SaveRoleRequest request, CancellationToken cancellationToken)
    {
        if (!User.HasClaim("permission", PermissionKeys.RolesModifyAccess))
        {
            return Forbid();
        }
        return await RunAsync(() => _roles.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("roles/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!User.HasClaim("permission", PermissionKeys.RolesModifyAccess))
        {
            return Forbid();
        }
        await _roles.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> GetPermissions(CancellationToken cancellationToken) =>
        Ok(await _roles.GetPermissionsAsync(cancellationToken));

    // The catalog is fixed: nobody, including an Administrator, can add, change or remove a permission.
    [HttpPost("permissions")]
    [HttpPut("permissions/{id:int}")]
    [HttpDelete("permissions/{id:int}")]
    public IActionResult CatalogIsFixed() =>
        Problem(detail: "The permission list is fixed and cannot be changed.", statusCode: StatusCodes.Status403Forbidden);

    private async Task<ActionResult<RoleDto>> RunAsync(Func<Task<RoleDto>> action)
    {
        return Ok(await action());
    }
}
