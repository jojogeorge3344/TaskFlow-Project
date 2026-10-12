namespace TaskFlow.Application.Dashboard;

public interface IDashboardService
{
    Task<AdminDashboardDto> GetAdminStatsAsync(CancellationToken cancellationToken);
    Task<ManagerDashboardDto> GetManagerStatsAsync(CancellationToken cancellationToken);
    Task<EmployeeDashboardDto> GetEmployeeStatsAsync(int userId, CancellationToken cancellationToken);
}
