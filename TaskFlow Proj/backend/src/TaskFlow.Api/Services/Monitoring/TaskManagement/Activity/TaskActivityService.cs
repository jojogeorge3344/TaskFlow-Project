using TaskFlow.Api.Dtos.Monitoring.TaskManagement.Activity;
using TaskFlow.Api.Common.Exceptions;
using System.Globalization;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Monitoring.TaskManagement.Activity;
using Task = System.Threading.Tasks.Task;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Monitoring.TaskManagement.Tasks;

namespace TaskFlow.Api.Services.Monitoring.TaskManagement.Activity;

/// <summary>Activity history. Entries are append-only, except that an administrator may correct or remove a worked-hours entry.</summary>
public class TaskActivityService : ITaskActivityService
{
    private readonly ITaskActivityRepository _activity;
    private readonly ITaskRepository _tasks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public TaskActivityService(ITaskActivityRepository activity, ITaskRepository tasks, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _activity = activity;
        _tasks = tasks;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task AppendAsync(
        int taskId,
        TaskActivityEventType eventType,
        int actorUserId,
        string message,
        string? oldValue = null,
        string? newValue = null,
        CancellationToken cancellationToken = default)
    {
        _activity.Add(new TaskActivityLog
        {
            TaskId = taskId,
            EventType = eventType,
            ActorUserId = actorUserId,
            Message = message,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ActivityEntryDto>> GetForTaskAsync(
        int taskId,
        ActivityFilter filter,
        CancellationToken cancellationToken)
    {
        var entries = await _activity.GetForTaskAsync(taskId, filter, cancellationToken);

        return entries
            .Select(log => new ActivityEntryDto(
                log.Id,
                log.EventType.ToString(),
                log.ActorUserId,
                log.Actor?.FullName ?? string.Empty,
                log.OldValue,
                log.NewValue,
                log.Message,
                log.CreatedAt))
            .ToList();
    }

    public Task<bool> UpdateHoursEntryAsync(int taskId, int entryId, decimal hours, int actorUserId, CancellationToken cancellationToken)
    {
        if (hours <= 0 || hours > 24)
        {
            throw new BusinessRuleException("Hours must be greater than 0 and at most 24 per entry.");
        }

        return ChangeHoursEntryAsync(taskId, entryId, hours, actorUserId, cancellationToken);
    }

    public Task<bool> DeleteHoursEntryAsync(int taskId, int entryId, int actorUserId, CancellationToken cancellationToken) =>
        ChangeHoursEntryAsync(taskId, entryId, null, actorUserId, cancellationToken);

    /// <summary>Each hours entry stores the task's worked total before and after it, so an entry adds (new - old) hours.
    /// A change sets or drops that amount and replays all of the task's hours entries to rebuild the totals.</summary>
    private async Task<bool> ChangeHoursEntryAsync(
        int taskId,
        int entryId,
        decimal? newHours,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        var task = await _tasks.GetLiveForUpdateAsync(taskId, cancellationToken);
        if (task is null)
        {
            return false;
        }

        var entries = await _activity.GetHoursEntriesForUpdateAsync(taskId, cancellationToken);

        var target = entries.FirstOrDefault(entry => entry.Id == entryId);
        if (target is null)
        {
            return false;
        }

        var deltas = entries.ToDictionary(entry => entry.Id, entry => ParseHours(entry.NewValue) - ParseHours(entry.OldValue));
        var oldDelta = deltas[target.Id];
        if (newHours is not null && oldDelta <= 0)
        {
            throw new BusinessRuleException("This entry did not add hours, so it can only be deleted.");
        }

        var baseline = ParseHours(entries[0].OldValue);
        var what = newHours is null
            ? $"Administrator deleted a worked-hours entry of {Format(oldDelta)}h"
            : $"Administrator changed a worked-hours entry from {Format(oldDelta)}h to {Format(newHours.Value)}h";

        if (newHours is null)
        {
            _activity.Remove(target);
            entries.Remove(target);
        }
        else
        {
            deltas[target.Id] = newHours.Value;
        }

        var running = baseline;
        foreach (var entry in entries)
        {
            var next = Math.Max(0, running + deltas[entry.Id]);
            entry.OldValue = Format(running);
            entry.NewValue = Format(next);
            entry.Message = $"Worked hours logged: {entry.NewValue}h (total)";
            running = next;
        }

        var previousTotal = task.ActualHours ?? 0;
        task.ActualHours = running;
        task.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        _activity.Add(new TaskActivityLog
        {
            TaskId = taskId,
            EventType = TaskActivityEventType.Administrative,
            ActorUserId = actorUserId,
            Message = $"{what}; worked total is now {Format(running)}h",
            OldValue = Format(previousTotal),
            NewValue = Format(running),
            CreatedAt = task.UpdatedAt
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal ParseHours(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
