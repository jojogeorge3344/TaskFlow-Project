using TaskFlow.Api.Services.Interfaces.Monitoring.EmployeeWorkload;
using TaskFlow.Api.Dtos.Monitoring.EmployeeWorkload;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.Api.Controllers.Monitoring.EmployeeWorkload;

/// <summary>Employee workload (Screen 16). Needs workload.view.</summary>
[ApiController]
[Route("api/v1/workload")]
[RequirePermission(PermissionKeys.WorkloadView)]
public class WorkloadController : ControllerBase
{
    private readonly IWorkloadService _workloadService;

    public WorkloadController(IWorkloadService workloadService)
    {
        _workloadService = workloadService;
    }

    /// <summary>Per-employee task load, optionally narrowed to one project.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EmployeeWorkloadDto>>> Get(
        [FromQuery] int? projectId,
        CancellationToken cancellationToken)
    {
        var workload = await _workloadService.GetWorkloadAsync(projectId, cancellationToken);
        return Ok(workload);
    }
}
