using TaskFlow.Api.Dtos.Dashboard;

namespace TaskFlow.Api.Services.Dashboard;

public interface IDashboardService
{
    Task<AdminDashboardDto> GetAdminStatsAsync(CancellationToken cancellationToken);
    /// <summary>Counters for the manager's own team only (direct reports plus managed-department members).</summary>
    Task<ManagerDashboardDto> GetManagerStatsAsync(int managerId, CancellationToken cancellationToken);
    Task<EmployeeDashboardDto> GetEmployeeStatsAsync(int userId, CancellationToken cancellationToken);
}
