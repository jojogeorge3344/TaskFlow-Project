namespace TaskFlow.Api.Repositories.Interfaces.Monitoring.TaskManagement;

/// <summary>Tasks. Grows with the Task Management module; for now it answers the questions other modules ask about tasks.</summary>
public interface ITaskRepository : IBaseRepository<TaskFlow.Api.Models.Entities.Task>
{
    /// <summary>How many live tasks assigned to this person are neither completed nor cancelled.</summary>
    Task<int> CountUnfinishedAssignedToAsync(int userId, CancellationToken cancellationToken = default);
}
