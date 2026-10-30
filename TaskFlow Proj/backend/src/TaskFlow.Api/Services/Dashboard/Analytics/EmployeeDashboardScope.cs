using TaskFlow.Api.Repositories.Interfaces.Dashboard.Analytics;
using TaskFlow.Api.Services.Interfaces.Dashboard.Analytics;

namespace TaskFlow.Api.Services.Dashboard.Analytics;

public class EmployeeDashboardScope : IEmployeeDashboardScope
{
    public AnalyticsTaskSet OwnTasks(int userId) => new(OwnUserId: userId);
}
