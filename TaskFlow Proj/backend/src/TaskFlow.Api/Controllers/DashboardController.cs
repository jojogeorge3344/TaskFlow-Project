using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Dashboard;

namespace TaskFlow.Api.Controllers;

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
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<AdminDashboardDto>> GetAdminStats(CancellationToken cancellationToken)
    {
        var stats = await _dashboardService.GetAdminStatsAsync(cancellationToken);
        return Ok(stats);
    }

    [HttpGet("manager")]
    [Authorize(Policy = AuthPolicies.ManagerOrAdmin)]
    public async Task<ActionResult<ManagerDashboardDto>> GetManagerStats(CancellationToken cancellationToken)
    {
        var stats = await _dashboardService.GetManagerStatsAsync(cancellationToken);
        return Ok(stats);
    }

    [HttpGet("employee")]
    [Authorize]
    public async Task<ActionResult<EmployeeDashboardDto>> GetEmployeeStats(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }
        var stats = await _dashboardService.GetEmployeeStatsAsync(userId, cancellationToken);
        return Ok(stats);
    }
}
