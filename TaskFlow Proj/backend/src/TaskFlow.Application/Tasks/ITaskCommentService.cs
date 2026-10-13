namespace TaskFlow.Application.Tasks;

public interface ITaskCommentService
{
    /// <summary>Chronological (oldest first) comments for a task, or an empty list when the
    /// task has no comments. Access is asserted by the caller.</summary>
    Task<IReadOnlyCollection<TaskCommentDto>> GetForTaskAsync(int taskId, CancellationToken cancellationToken);

    /// <summary>Adds an immutable comment. Throws when the body is empty or the task does
    /// not exist.</summary>
    Task<TaskCommentDto> AddAsync(int taskId, AddCommentRequest request, int authorUserId, CancellationToken cancellationToken);
}