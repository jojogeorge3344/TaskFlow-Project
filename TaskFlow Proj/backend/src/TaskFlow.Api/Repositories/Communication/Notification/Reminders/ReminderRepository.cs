using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Repositories.Communication.Notification.Reminders;

public class ReminderRepository : BaseRepository<ReminderHistory>, IReminderRepository
{
    public ReminderRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<ReminderCandidate>> GetOpenTasksDueByAsync(DateTime dueBy, CancellationToken cancellationToken = default) =>
        await Db.Tasks
            .AsNoTracking()
            .Where(task => !task.IsDeleted
                && task.Status != TaskStatus.Completed
                && task.Status != TaskStatus.Cancelled
                && task.Deadline <= dueBy)
            .Select(task => new ReminderCandidate(
                task.Id,
                task.Title,
                task.CreatedByUserId,
                new ReminderTaskSnapshot(
                    task.Status,
                    task.Deadline,
                    task.FirstReminderDays,
                    task.SecondReminderDays,
                    task.OverdueReminderFrequency)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DeliveredReminder>> GetDeliveredAsync(
        IReadOnlyCollection<int> taskIds,
        DateTime earliestSlot,
        CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(row => taskIds.Contains(row.TaskId) && row.ScheduledFor >= earliestSlot)
            .Select(row => new DeliveredReminder(row.TaskId, row.RecipientUserId, row.ReminderType, row.ScheduledFor))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskRecipientLink>> GetTaskRecipientsAsync(IReadOnlyCollection<int> taskIds, CancellationToken cancellationToken = default) =>
        await Db.TaskRecipients
            .AsNoTracking()
            .Where(recipient => taskIds.Contains(recipient.TaskId))
            .Select(recipient => new TaskRecipientLink(recipient.TaskId, recipient.UserId))
            .ToListAsync(cancellationToken);

    public async Task<bool> TryRecordAsync(
        int taskId,
        int recipientId,
        ReminderType type,
        DateTime scheduledForUtc,
        DateTime deadlineUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var reminderType = type.ToString();
        var sent = ReminderStatus.Sent.ToString();
        // Task.Status is stored as its integer value (no string conversion on Tasks).
        var completed = (int)TaskStatus.Completed;
        var cancelled = (int)TaskStatus.Cancelled;
        var inserted = await Db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "ReminderHistory" ("TaskId", "RecipientUserId", "ReminderType", "ScheduledFor", "Status", "ProcessedAt", "DeliveryAttempts")
            SELECT {taskId}, {recipientId}, {reminderType}, {scheduledForUtc}, {sent}, {nowUtc}, 0
            WHERE EXISTS (
                SELECT 1 FROM "Tasks"
                WHERE "Id" = {taskId}
                  AND "IsDeleted" = 0
                  AND "Status" NOT IN ({completed}, {cancelled})
                  AND "Deadline" = {deadlineUtc})
            ON CONFLICT ("TaskId", "RecipientUserId", "ReminderType", "ScheduledFor") DO NOTHING
            """,
            cancellationToken);
        return inserted > 0;
    }

    public Task<int> GetIdAsync(int taskId, int recipientId, ReminderType type, DateTime scheduledForUtc, CancellationToken cancellationToken = default) =>
        Set.Where(row => row.TaskId == taskId
                && row.RecipientUserId == recipientId
                && row.ReminderType == type
                && row.ScheduledFor == scheduledForUtc)
            .Select(row => row.Id)
            .SingleAsync(cancellationToken);
}
