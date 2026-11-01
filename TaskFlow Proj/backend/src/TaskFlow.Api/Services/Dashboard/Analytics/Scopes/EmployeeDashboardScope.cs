using TaskFlow.Api.Repositories.Dashboard.Analytics;

namespace TaskFlow.Api.Services.Dashboard.Analytics.Scopes;

public class EmployeeDashboardScope : IEmployeeDashboardScope
{
    public AnalyticsTaskSet OwnTasks(int userId) => new(OwnUserId: userId);
}
