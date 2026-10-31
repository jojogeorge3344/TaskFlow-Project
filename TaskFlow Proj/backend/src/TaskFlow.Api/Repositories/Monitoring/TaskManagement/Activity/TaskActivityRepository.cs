using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Dtos.Monitoring.TaskManagement.Activity;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Monitoring.TaskManagement.Activity;

public class TaskActivityRepository : BaseRepository<TaskActivityLog>, ITaskActivityRepository
{
    public TaskActivityRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<TaskActivityLog>> GetForTaskAsync(int taskId, ActivityFilter filter, CancellationToken cancellationToken = default)
    {
        IQueryable<TaskActivityLog> query = Set.AsNoTracking().Where(log => log.TaskId == taskId);

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

        return await query
            .IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Include(log => log.Actor)
            .OrderBy(log => log.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<TaskActivityLog>> GetHoursEntriesForUpdateAsync(int taskId, CancellationToken cancellationToken = default) =>
        Set.Where(log => log.TaskId == taskId && log.EventType == TaskActivityEventType.HoursLogged)
            .OrderBy(log => log.CreatedAt)
            .ThenBy(log => log.Id)
            .ToListAsync(cancellationToken);
}
