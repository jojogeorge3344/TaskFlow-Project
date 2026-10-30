using TaskFlow.Api.Dtos.Monitoring.TaskManagement.Activity;
using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Interfaces.Monitoring.TaskManagement.Activity;

/// <summary>A task's activity history.</summary>
public interface ITaskActivityRepository : IBaseRepository<TaskActivityLog>
{
    /// <summary>The task's entries matching the filter, oldest first, with their actors (deleted accounts included).</summary>
    Task<IReadOnlyList<TaskActivityLog>> GetForTaskAsync(int taskId, ActivityFilter filter, CancellationToken cancellationToken = default);

    /// <summary>The task's tracked worked-hours entries, in the order they were logged.</summary>
    Task<List<TaskActivityLog>> GetHoursEntriesForUpdateAsync(int taskId, CancellationToken cancellationToken = default);
}
