using TaskFlow.Api.Repositories.Dashboard.Analytics;

namespace TaskFlow.Api.Services.Dashboard.Analytics.Scopes;

/// <summary>The Employee's scope: the tasks assigned to or shared with the caller. The caller's id always comes from the
/// session, so the scope cannot be pointed at anyone else.</summary>
public interface IEmployeeDashboardScope
{
    /// <summary>The counted tasks assigned to or shared with <paramref name="userId"/>.</summary>
    AnalyticsTaskSet OwnTasks(int userId);
}
