using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tasks;

public interface ITaskActivityService
{
    /// <summary>Appends an immutable activity entry. Callers own the transaction.</summary>
    Task AppendAsync(int taskId, TaskActivityEventType eventType, int actorUserId, string message, string? oldValue = null, string? newValue = null, CancellationToken cancellationToken = default);

    /// <summary>Chronological (oldest first) activity entries for a task, optionally filtered.</summary>
    Task<IReadOnlyCollection<ActivityEntryDto>> GetForTaskAsync(int taskId, ActivityFilter filter, CancellationToken cancellationToken);
}