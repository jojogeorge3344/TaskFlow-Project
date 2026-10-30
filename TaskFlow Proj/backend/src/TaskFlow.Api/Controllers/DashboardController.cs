using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;
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

    private readonly IEmployeeAnalyticsService _employeeAnalyticsService;

    public DashboardController(
        IDashboardService dashboardService,
        IAdminAnalyticsService analyticsService,
        IManagerAnalyticsService managerAnalyticsService,
        IEmployeeAnalyticsService employeeAnalyticsService)
    {
        _dashboardService = dashboardService;
        _analyticsService = analyticsService;
        _managerAnalyticsService = managerAnalyticsService;
        _employeeAnalyticsService = employeeAnalyticsService;
    }

    [HttpGet("admin")]
    [RequirePermission(PermissionKeys.DashboardViewOrganization)]
    public async Task<ActionResult<AdminDashboardDto>> GetAdminStats(CancellationToken cancellationToken)
    {
        var stats = await _dashboardService.GetAdminStatsAsync(cancellationToken);
        return Ok(stats);
    }

    [HttpGet("admin/analytics")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.DashboardAnalytics)]
    [RequirePermission(PermissionKeys.DashboardViewOrganization)]
    public async Task<ActionResult<AdminAnalyticsDto>> GetAdminAnalytics(
        [FromQuery] AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _analyticsService.GetAsync(filter, cancellationToken));
    }

    [HttpGet("admin/analytics/filters")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.DashboardFilters)]
    [RequirePermission(PermissionKeys.DashboardViewOrganization)]
    public async Task<ActionResult<AnalyticsFilterOptionsDto>> GetAdminAnalyticsFilters(CancellationToken cancellationToken)
    {
        return Ok(await _analyticsService.GetFilterOptionsAsync(cancellationToken));
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

    [HttpGet("manager/analytics")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.DashboardAnalytics)]
    [RequirePermission(PermissionKeys.DashboardViewTeam)]
    public async Task<ActionResult<ManagerAnalyticsResponseDto>> GetManagerAnalytics(
        [FromQuery] AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var managerId))
        {
            return Unauthorized();
        }

        return Ok(await _managerAnalyticsService.GetAsync(managerId, filter, cancellationToken));
    }

    [HttpGet("manager/analytics/filters")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.DashboardFilters)]
    [RequirePermission(PermissionKeys.DashboardViewTeam)]
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

    /// <summary>Analytics of the caller's own tasks. The subject is always the session user; the only input is the
    /// period, so any other query key (an employee id, a department) is never read.</summary>
    [HttpGet("employee/analytics")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.DashboardAnalytics)]
    [Authorize]
    public async Task<ActionResult<EmployeeAnalyticsResponseDto>> GetEmployeeAnalytics(
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _employeeAnalyticsService.GetAsync(userId, period, cancellationToken));
    }

    private bool TryGetCallerId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
