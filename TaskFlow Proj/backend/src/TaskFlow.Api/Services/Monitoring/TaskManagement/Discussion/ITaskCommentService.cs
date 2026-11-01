using TaskFlow.Api.Dtos.Monitoring.TaskManagement.Discussion;

namespace TaskFlow.Api.Services.Monitoring.TaskManagement.Discussion;

public interface ITaskCommentService
{
    /// <summary>Chronological (oldest first) comments for a task, or an empty list when the
    /// task has no comments. Access is asserted by the caller.</summary>
    Task<IReadOnlyCollection<TaskCommentDto>> GetForTaskAsync(int taskId, CancellationToken cancellationToken);

    /// <summary>Adds a comment. Throws when the body is empty or the task does
    /// not exist.</summary>
    Task<TaskCommentDto> AddAsync(int taskId, AddCommentRequest request, int authorUserId, CancellationToken cancellationToken);

    /// <summary>Author-only edit. Returns null when the comment does not exist on the task; throws
    /// UnauthorizedAccessException for a non-author and InvalidOperationException for an empty body or missing task.</summary>
    Task<TaskCommentDto?> UpdateAsync(int taskId, int commentId, AddCommentRequest request, int actorUserId, CancellationToken cancellationToken);

    /// <summary>Author-only soft delete. Returns false when the comment does not exist on the task.</summary>
    Task<bool> DeleteAsync(int taskId, int commentId, int actorUserId, CancellationToken cancellationToken);
}