namespace TaskFlow.Api.Services.Monitoring.TaskManagement.Access;

/// <summary>The resource-level rule behind task-scoped endpoints: may this caller reach this task?</summary>
public interface ITaskAccessService
{
    /// <summary>False when the task does not exist or is deleted. Otherwise true for a caller who may view all tasks, and for
    /// the assignee, the creator and any additional recipient.</summary>
    Task<bool> CanAccessAsync(int taskId, int? userId, bool canViewAll, CancellationToken cancellationToken = default);
}
