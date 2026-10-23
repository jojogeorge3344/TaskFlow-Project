using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Domain.Security;
using TaskFlow.Application.Dashboard;
using TaskFlow.Application.Dashboard.Analytics;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IAdminAnalyticsService _analyticsService;

    public DashboardController(IDashboardService dashboardService, IAdminAnalyticsService analyticsService)
    {
        _dashboardService = dashboardService;
        _analyticsService = analyticsService;
    }

    [HttpGet("admin")]
    [RequirePermission(PermissionKeys.DashboardAdmin)]
    public async Task<ActionResult<AdminDashboardDto>> GetAdminStats(CancellationToken cancellationToken)
    {
        var stats = await _dashboardService.GetAdminStatsAsync(cancellationToken);
        return Ok(stats);
    }

    [HttpGet("admin/analytics")]
    [RequirePermission(PermissionKeys.DashboardAdmin)]
    public async Task<ActionResult<AdminAnalyticsDto>> GetAdminAnalytics(
        [FromQuery] AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _analyticsService.GetAsync(filter, cancellationToken));
        }
        catch (AnalyticsFilterException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (AnalyticsEntityNotFoundException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    [HttpGet("admin/analytics/filters")]
    [RequirePermission(PermissionKeys.DashboardAdmin)]
    public async Task<ActionResult<AnalyticsFilterOptionsDto>> GetAdminAnalyticsFilters(CancellationToken cancellationToken)
    {
        return Ok(await _analyticsService.GetFilterOptionsAsync(cancellationToken));
    }

    [HttpGet("manager")]
    [RequirePermission(PermissionKeys.DashboardManager)]
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
