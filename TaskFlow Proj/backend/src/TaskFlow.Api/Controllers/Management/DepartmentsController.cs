using TaskFlow.Api.Services.Management.DepartmentManagement;
using TaskFlow.Api.Dtos.Management.DepartmentManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.Api.Controllers.Management;

[ApiController]
[Route("api/v1/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(
        IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.DepartmentsView)]
    public async Task<ActionResult<IReadOnlyCollection<DepartmentWithCountsDto>>> GetAll(CancellationToken cancellationToken)
    {
        var departments = await _departmentService.GetAllAsync(cancellationToken);
        return Ok(departments);
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionKeys.DepartmentsView)]
    public async Task<ActionResult<DepartmentDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var department = await _departmentService.GetByIdAsync(id, cancellationToken);
        if (department is null)
        {
            return NotFound();
        }
        return Ok(department);
    }

    [HttpGet("{id}/members")]
    [RequirePermission(PermissionKeys.DepartmentsView)]
    public async Task<ActionResult<DepartmentMembersDto>> GetMembers(int id, CancellationToken cancellationToken)
    {
        var members = await _departmentService.GetMembersAsync(id, cancellationToken);
        if (members is null)
        {
            return NotFound();
        }
        return Ok(members);
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.DepartmentsCreate)]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _departmentService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = department.Id }, department);
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionKeys.DepartmentsEdit)]
    public async Task<ActionResult<DepartmentDto>> Update(int id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _departmentService.UpdateAsync(id, request, cancellationToken);
        if (department is null)
        {
            return NotFound();
        }
        return Ok(department);
    }

    [HttpPut("{id}/members")]
    [RequirePermission(PermissionKeys.DepartmentsMembers)]
    public async Task<ActionResult<DepartmentMembersDto>> ReplaceMembers(int id, [FromBody] UpdateDepartmentMembersRequest request, CancellationToken cancellationToken)
    {
        var members = await _departmentService.ReplaceMembersAsync(id, request, cancellationToken);
        if (members is null)
        {
            return NotFound();
        }
        return Ok(members);
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionKeys.DepartmentsDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _departmentService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
