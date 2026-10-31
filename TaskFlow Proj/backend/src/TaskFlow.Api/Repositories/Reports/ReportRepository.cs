using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Dtos.Reports;
using TaskFlow.Api.Persistence;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Repositories.Reports;

public class ReportRepository : BaseRepository<TaskEntity>, IReportRepository
{
    public ReportRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<ReportSummaryDto> GetSummaryAsync(ReportCriteria criteria, DateTime now, CancellationToken cancellationToken = default)
    {
        var summary = await Covered(criteria)
            .Select(task => new
            {
                Total = 1,
                Pending = task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled ? 1 : 0,
                Completed = task.Status == TaskStatus.Completed ? 1 : 0,
                Overdue = task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled && task.Deadline < now
                    ? 1
                    : 0
            })
            .GroupBy(_ => 1)
            .Select(group => new ReportSummaryDto(
                group.Sum(row => row.Total),
                group.Sum(row => row.Pending),
                group.Sum(row => row.Completed),
                group.Sum(row => row.Overdue)))
            .SingleOrDefaultAsync(cancellationToken);

        return summary ?? new ReportSummaryDto(0, 0, 0, 0);
    }

    public async Task<IReadOnlyList<ReportTaskRow>> GetRowsAsync(ReportCriteria criteria, int take, CancellationToken cancellationToken = default) =>
        await Covered(criteria)
            .OrderBy(task => task.Deadline)
            .ThenBy(task => task.Id)
            .Take(take)
            .Select(task => new ReportTaskRow(
                task.TaskCode,
                task.Title,
                task.AssignedTo == null ? string.Empty : task.AssignedTo.FullName,
                task.AssignedToUserId,
                task.Priority,
                task.Status,
                task.StartDate,
                task.Deadline,
                task.EstimatedHours,
                task.CompletedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetProjectNamesByUserAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default) =>
        (await Db.UserProjectAllocations
            .IgnoreQueryFilters()
            .Where(allocation => userIds.Contains(allocation.UserId))
            .OrderBy(allocation => allocation.Project.Name)
            .Select(allocation => new { allocation.UserId, allocation.Project.Name })
            .ToListAsync(cancellationToken))
        .GroupBy(allocation => allocation.UserId)
        .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(allocation => allocation.Name).ToList());

    private IQueryable<TaskEntity> Covered(ReportCriteria criteria)
    {
        // Users are soft-deleted behind a query filter; reports must keep a deleted person's finished work and name.
        var tasks = Set.AsNoTracking().IgnoreQueryFilters().Where(task => !task.IsDeleted);

        if (criteria.DeadlineFromUtc is DateTime from)
        {
            tasks = tasks.Where(task => task.Deadline >= from);
        }

        if (criteria.DeadlineToUtc is DateTime to)
        {
            tasks = tasks.Where(task => task.Deadline <= to);
        }

        if (criteria.AssignedToUserId is int assignee)
        {
            tasks = tasks.Where(task => task.AssignedToUserId == assignee);
        }

        if (criteria.ProjectId is int project)
        {
            tasks = tasks.Where(task => task.AssignedTo != null
                && task.AssignedTo.ProjectAllocations.Any(allocation => allocation.ProjectId == project));
        }

        if (criteria.Status is { } status)
        {
            tasks = tasks.Where(task => task.Status == status);
        }

        if (criteria.Priority is { } priority)
        {
            tasks = tasks.Where(task => task.Priority == priority);
        }

        return tasks;
    }
}
