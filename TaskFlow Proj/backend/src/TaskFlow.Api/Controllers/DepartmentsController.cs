using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Domain.Security;
using TaskFlow.Application.Departments;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/v1/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;
    private readonly IValidator<CreateDepartmentRequest> _createValidator;
    private readonly IValidator<UpdateDepartmentRequest> _updateValidator;

    public DepartmentsController(
        IDepartmentService departmentService,
        IValidator<CreateDepartmentRequest> createValidator,
        IValidator<UpdateDepartmentRequest> updateValidator)
    {
        _departmentService = departmentService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
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
    [RequirePermission(PermissionKeys.DepartmentsManage)]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

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
    [RequirePermission(PermissionKeys.DepartmentsManage)]
    public async Task<ActionResult<DepartmentDto>> Update(int id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

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

    [HttpPut("{id}/members")]
    [RequirePermission(PermissionKeys.DepartmentsManage)]
    public async Task<ActionResult<DepartmentMembersDto>> ReplaceMembers(int id, [FromBody] UpdateDepartmentMembersRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var members = await _departmentService.ReplaceMembersAsync(id, request, cancellationToken);
            if (members is null)
            {
                return NotFound();
            }
            return Ok(members);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionKeys.DepartmentsManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _departmentService.DeleteAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
