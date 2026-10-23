namespace TaskFlow.Application.Dashboard.Analytics;

/// <summary>The Employee's scope: the tasks assigned to or shared with the caller. The caller's id always comes from the
/// session, so the scope cannot be pointed at anyone else.</summary>
public interface IEmployeeDashboardScope
{
    /// <summary>Restricts a task query to counted tasks assigned to or shared with <paramref name="userId"/>.</summary>
    IQueryable<TaskFlow.Domain.Entities.Task> OwnTasks(IQueryable<TaskFlow.Domain.Entities.Task> tasks, int userId);
}
