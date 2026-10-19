using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Domain.Security;
using TaskFlow.Application.Workload;

namespace TaskFlow.Api.Controllers;

/// <summary>Employee workload (Screen 16). Administrator and Manager only.</summary>
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

    /// <summary>Per-employee task load, optionally narrowed to one department.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EmployeeWorkloadDto>>> Get(
        [FromQuery] int? departmentId,
        CancellationToken cancellationToken)
    {
        var workload = await _workloadService.GetWorkloadAsync(departmentId, cancellationToken);
        return Ok(workload);
    }
}
