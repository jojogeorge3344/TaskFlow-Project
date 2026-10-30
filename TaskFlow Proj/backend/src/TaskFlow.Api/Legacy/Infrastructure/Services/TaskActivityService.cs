using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Activity history. Entries are append-only, except that an administrator may correct or remove a worked-hours entry.</summary>
public class TaskActivityService : ITaskActivityService
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public TaskActivityService(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
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
        _dbContext.TaskActivityLogs.Add(new TaskActivityLog
        {
            TaskId = taskId,
            EventType = eventType,
            ActorUserId = actorUserId,
            Message = message,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ActivityEntryDto>> GetForTaskAsync(
        int taskId,
        ActivityFilter filter,
        CancellationToken cancellationToken)
    {
        IQueryable<TaskActivityLog> query = _dbContext.TaskActivityLogs
            .AsNoTracking()
            .Where(log => log.TaskId == taskId);

        if (filter.EventType is not null)
        {
            query = query.Where(log => log.EventType == filter.EventType);
        }

        if (filter.ActorUserId is not null)
        {
            query = query.Where(log => log.ActorUserId == filter.ActorUserId);
        }

        if (filter.From is not null)
        {
            query = query.Where(log => log.CreatedAt >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(log => log.CreatedAt <= filter.To);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(log => log.Message.Contains(filter.Search));
        }

        var entries = await query
            .IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Include(log => log.Actor)
            .OrderBy(log => log.CreatedAt)
            .ToListAsync(cancellationToken);

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
            throw new InvalidOperationException("Hours must be greater than 0 and at most 24 per entry.");
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
        var task = await _dbContext.Tasks.FirstOrDefaultAsync(candidate => candidate.Id == taskId && !candidate.IsDeleted, cancellationToken);
        if (task is null)
        {
            return false;
        }

        var entries = await _dbContext.TaskActivityLogs
            .Where(log => log.TaskId == taskId && log.EventType == TaskActivityEventType.HoursLogged)
            .OrderBy(log => log.CreatedAt)
            .ThenBy(log => log.Id)
            .ToListAsync(cancellationToken);

        var target = entries.FirstOrDefault(entry => entry.Id == entryId);
        if (target is null)
        {
            return false;
        }

        var deltas = entries.ToDictionary(entry => entry.Id, entry => ParseHours(entry.NewValue) - ParseHours(entry.OldValue));
        var oldDelta = deltas[target.Id];
        if (newHours is not null && oldDelta <= 0)
        {
            throw new InvalidOperationException("This entry did not add hours, so it can only be deleted.");
        }

        var baseline = ParseHours(entries[0].OldValue);
        var what = newHours is null
            ? $"Administrator deleted a worked-hours entry of {Format(oldDelta)}h"
            : $"Administrator changed a worked-hours entry from {Format(oldDelta)}h to {Format(newHours.Value)}h";

        if (newHours is null)
        {
            _dbContext.TaskActivityLogs.Remove(target);
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

        _dbContext.TaskActivityLogs.Add(new TaskActivityLog
        {
            TaskId = taskId,
            EventType = TaskActivityEventType.Administrative,
            ActorUserId = actorUserId,
            Message = $"{what}; worked total is now {Format(running)}h",
            OldValue = Format(previousTotal),
            NewValue = Format(running),
            CreatedAt = task.UpdatedAt
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal ParseHours(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
