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

    private readonly IManagerAnalyticsService _managerAnalyticsService;

    public DashboardController(
        IDashboardService dashboardService,
        IAdminAnalyticsService analyticsService,
        IManagerAnalyticsService managerAnalyticsService)
    {
        _dashboardService = dashboardService;
        _analyticsService = analyticsService;
        _managerAnalyticsService = managerAnalyticsService;
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
        if (!TryGetCallerId(out var managerId))
        {
            return Unauthorized();
        }

        var stats = await _dashboardService.GetManagerStatsAsync(managerId, cancellationToken);
        return Ok(stats);
    }

    [HttpGet("manager/analytics")]
    [RequirePermission(PermissionKeys.DashboardManager)]
    public async Task<ActionResult<ManagerAnalyticsResponseDto>> GetManagerAnalytics(
        [FromQuery] AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var managerId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _managerAnalyticsService.GetAsync(managerId, filter, cancellationToken));
        }
        catch (AnalyticsFilterException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (AnalyticsForbiddenException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AnalyticsEntityNotFoundException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    [HttpGet("manager/analytics/filters")]
    [RequirePermission(PermissionKeys.DashboardManager)]
    public async Task<ActionResult<ManagerFilterOptionsDto>> GetManagerAnalyticsFilters(CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var managerId))
        {
            return Unauthorized();
        }

        return Ok(await _managerAnalyticsService.GetFilterOptionsAsync(managerId, cancellationToken));
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
