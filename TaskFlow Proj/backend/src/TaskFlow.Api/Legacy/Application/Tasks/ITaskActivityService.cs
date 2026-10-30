using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Application.Tasks;

public interface ITaskActivityService
{
    /// <summary>Appends an immutable activity entry. Callers own the transaction.</summary>
    Task AppendAsync(int taskId, TaskActivityEventType eventType, int actorUserId, string message, string? oldValue = null, string? newValue = null, CancellationToken cancellationToken = default);

    /// <summary>Chronological (oldest first) activity entries for a task, optionally filtered.</summary>
    Task<IReadOnlyCollection<ActivityEntryDto>> GetForTaskAsync(int taskId, ActivityFilter filter, CancellationToken cancellationToken);

    /// <summary>Administrator correction of one "worked hours logged" entry: sets the hours that entry added, rebuilds the
    /// running totals of the task's hour entries, updates the task's worked total and records an Administrative entry.
    /// Returns false when the entry is not an hours entry of that task. Throws InvalidOperationException for hours that are not positive
    /// or an entry that did not add hours.</summary>
    Task<bool> UpdateHoursEntryAsync(int taskId, int entryId, decimal hours, int actorUserId, CancellationToken cancellationToken);

    /// <summary>Administrator removal of one "worked hours logged" entry; totals are rebuilt as for an edit.</summary>
    Task<bool> DeleteHoursEntryAsync(int taskId, int entryId, int actorUserId, CancellationToken cancellationToken);
}