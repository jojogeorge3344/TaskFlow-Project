using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Departments;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/v1/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet]
    [Authorize(Policy = AuthPolicies.ManagerOrAdmin)]
    public async Task<ActionResult<IReadOnlyCollection<DepartmentWithCountDto>>> GetAll(CancellationToken cancellationToken)
    {
        var departments = await _departmentService.GetAllAsync(cancellationToken);
        return Ok(departments);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = AuthPolicies.ManagerOrAdmin)]
    public async Task<ActionResult<DepartmentDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var department = await _departmentService.GetByIdAsync(id, cancellationToken);
        if (department is null)
        {
            return NotFound();
        }
        return Ok(department);
    }

    [HttpGet("{id}/employees")]
    [Authorize(Policy = AuthPolicies.ManagerOrAdmin)]
    public async Task<ActionResult<IReadOnlyCollection<Domain.Entities.User>>> GetEmployees(int id, CancellationToken cancellationToken)
    {
        var employees = await _departmentService.GetEmployeesAsync(id, cancellationToken);
        return Ok(employees);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var department = await _departmentService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = department.Id }, department);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<DepartmentDto>> Update(int id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var department = await _departmentService.UpdateAsync(id, request, cancellationToken);
            if (department is null)
            {
                return NotFound();
            }
            return Ok(department);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _departmentService.DeleteAsync(id, cancellationToken);
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
