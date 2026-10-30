using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;
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
    public async Task<ActionResult<IReadOnlyCollection<TaskFlow.Api.Models.Entities.User>>> GetEmployees(int id, CancellationToken cancellationToken)
    {
        var employees = await _projectService.GetEmployeesAsync(id, cancellationToken);
        return Ok(employees);
    }

    /// <summary>The project's team with the position and percentage of each member.</summary>
    [HttpGet("{id}/members")]
    [RequirePermission(PermissionKeys.ProjectsView)]
    public async Task<ActionResult<IReadOnlyCollection<ProjectMemberDto>>> GetMembers(int id, CancellationToken cancellationToken)
    {
        var members = await _projectService.GetMembersAsync(id, cancellationToken);
        return members is null ? NotFound() : Ok(members);
    }

    /// <summary>The project's tasks with each assignee's role on the project; filters combine with AND.</summary>
    [HttpGet("{id}/tasks")]
    [RequirePermission(PermissionKeys.ProjectsView)]
    public async Task<ActionResult<IReadOnlyCollection<ProjectTaskDto>>> GetTasks(
        int id,
        [FromQuery] ProjectTaskFilter filter,
        CancellationToken cancellationToken)
    {
        var tasks = await _projectService.GetTasksAsync(id, filter, cancellationToken);
        return tasks is null ? NotFound() : Ok(tasks);
    }

    /// <summary>Replaces the project's team. All-or-nothing: if anyone would pass 100% in total, nothing is saved.</summary>
    [HttpPut("{id}/members")]
    [RequirePermission(PermissionKeys.ProjectsMembers)]
    public async Task<ActionResult<IReadOnlyCollection<ProjectMemberDto>>> ReplaceMembers(
        int id,
        [FromBody] ReplaceProjectMembersRequest request,
        CancellationToken cancellationToken)
    {
        var members = await _projectService.ReplaceMembersAsync(id, request, cancellationToken);
        return members is null ? NotFound() : Ok(members);
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.ProjectsCreate)]
    public async Task<ActionResult<ProjectDto>> Create([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _projectService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionKeys.ProjectsEdit)]
    public async Task<ActionResult<ProjectDto>> Update(int id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _projectService.UpdateAsync(id, request, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }
        return Ok(project);
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionKeys.ProjectsDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _projectService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
