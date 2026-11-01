using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Monitoring.TaskManagement.Discussion;

/// <summary>A task's discussion comments. Deleted comments are flagged, never removed.</summary>
public interface ITaskCommentRepository : IBaseRepository<TaskComment>
{
    /// <summary>The task's comments that are not deleted, oldest first, with their authors (deleted accounts included).</summary>
    Task<IReadOnlyList<TaskComment>> GetForTaskAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>The tracked comment of the task, unless it is deleted.</summary>
    Task<TaskComment?> GetLiveForUpdateAsync(int taskId, int commentId, CancellationToken cancellationToken = default);
}
