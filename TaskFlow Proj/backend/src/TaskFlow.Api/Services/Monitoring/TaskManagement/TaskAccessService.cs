using TaskFlow.Api.Repositories.Monitoring.TaskManagement;
using TaskFlow.Api.Services.Monitoring.TaskManagement.Rules;

namespace TaskFlow.Api.Services.Monitoring.TaskManagement;

public class TaskAccessService : ITaskAccessService
{
    private readonly ITaskRepository _tasks;

    public TaskAccessService(ITaskRepository tasks)
    {
        _tasks = tasks;
    }

    public async Task<bool> CanAccessAsync(int taskId, int? userId, bool canViewAll, CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetLiveAsync(taskId, cancellationToken);
        if (task is null)
        {
            return false;
        }

        if (canViewAll)
        {
            return true;
        }

        if (userId is not int caller)
        {
            return false;
        }

        // The recipients are only read when the caller is neither the assignee nor the creator.
        return TaskAccessScope.CanAccess(caller, canViewAll: false, task, [])
            || TaskAccessScope.CanAccess(caller, canViewAll: false, task, await _tasks.GetRecipientIdsAsync(taskId, cancellationToken));
    }
}
