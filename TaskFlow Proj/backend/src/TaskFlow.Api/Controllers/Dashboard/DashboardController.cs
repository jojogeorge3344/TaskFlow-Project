using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Dashboard;
using TaskFlow.Api.Services.Dashboard;

namespace TaskFlow.Api.Controllers.Dashboard;

[ApiController]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("admin")]
    [RequirePermission(PermissionKeys.DashboardViewOrganization)]
    public async Task<ActionResult<AdminDashboardDto>> GetAdminStats(CancellationToken cancellationToken)
    {
        var stats = await _dashboardService.GetAdminStatsAsync(cancellationToken);
        return Ok(stats);
    }

    [HttpGet("manager")]
    [RequirePermission(PermissionKeys.DashboardViewTeam)]
    public async Task<ActionResult<ManagerDashboardDto>> GetManagerStats(CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var managerId))
        {
            return Unauthorized();
        }

        var stats = await _dashboardService.GetManagerStatsAsync(managerId, cancellationToken);
        return Ok(stats);
    }

    [HttpGet("employee")]
    [Authorize]
    public async Task<ActionResult<EmployeeDashboardDto>> GetEmployeeStats(CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }
        var stats = await _dashboardService.GetEmployeeStatsAsync(userId, cancellationToken);
        return Ok(stats);
    }

    private bool TryGetCallerId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
