using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Domain.Security;
using TaskFlow.Application.Projects;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/v1/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.ProjectsView)]
    public async Task<ActionResult<IReadOnlyCollection<ProjectWithCountDto>>> GetAll(CancellationToken cancellationToken)
    {
        var projects = await _projectService.GetAllAsync(cancellationToken);
        return Ok(projects);
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionKeys.ProjectsView)]
    public async Task<ActionResult<ProjectDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var project = await _projectService.GetByIdAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }
        return Ok(project);
    }

    [HttpGet("{id}/employees")]
    [RequirePermission(PermissionKeys.ProjectsView)]
    public async Task<ActionResult<IReadOnlyCollection<Domain.Entities.User>>> GetEmployees(int id, CancellationToken cancellationToken)
    {
        var employees = await _projectService.GetEmployeesAsync(id, cancellationToken);
        return Ok(employees);
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.ProjectsManage)]
    public async Task<ActionResult<ProjectDto>> Create([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var project = await _projectService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionKeys.ProjectsManage)]
    public async Task<ActionResult<ProjectDto>> Update(int id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var project = await _projectService.UpdateAsync(id, request, cancellationToken);
            if (project is null)
            {
                return NotFound();
            }
            return Ok(project);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionKeys.ProjectsManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _projectService.DeleteAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
