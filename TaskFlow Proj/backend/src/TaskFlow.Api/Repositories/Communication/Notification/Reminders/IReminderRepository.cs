using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Communication.Notification.Reminders;

/// <summary>The reminder history and what a reminder pass reads about tasks. The history's unique
/// (TaskId, RecipientUserId, ReminderType, ScheduledFor) index is what prevents duplicates.</summary>
public interface IReminderRepository : IBaseRepository<ReminderHistory>
{
    /// <summary>Live tasks that are neither completed nor cancelled and are due no later than <paramref name="dueBy"/>.</summary>
    Task<IReadOnlyList<ReminderCandidate>> GetOpenTasksDueByAsync(DateTime dueBy, CancellationToken cancellationToken = default);

    /// <summary>History rows of these tasks scheduled at or after <paramref name="earliestSlot"/>.</summary>
    Task<IReadOnlyList<DeliveredReminder>> GetDeliveredAsync(
        IReadOnlyCollection<int> taskIds,
        DateTime earliestSlot,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskRecipientLink>> GetTaskRecipientsAsync(IReadOnlyCollection<int> taskIds, CancellationToken cancellationToken = default);

    /// <summary>Inserts the history row unless it exists, and only while the task is still live, open and due at
    /// <paramref name="deadlineUtc"/>. Returns whether a row was added.</summary>
    Task<bool> TryRecordAsync(
        int taskId,
        int recipientId,
        ReminderType type,
        DateTime scheduledForUtc,
        DateTime deadlineUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<int> GetIdAsync(int taskId, int recipientId, ReminderType type, DateTime scheduledForUtc, CancellationToken cancellationToken = default);
}
