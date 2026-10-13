using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Append-only activity history. Entries are never updated or deleted.</summary>
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
}