using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Reports;
using TaskFlow.Application.Settings;
using TaskFlow.Infrastructure.Persistence;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;
using TaskEntity = TaskFlow.Domain.Entities.Task;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Filtered task report aggregation and CSV row projection (Screen 17). Organization-wide
/// for a Manager/Administrator, matching the dashboards' scoping (see the change design, D3), with the
/// shared status and overdue rules (D7). Department is the assignee's department.</summary>
public class ReportService : IReportService
{
    private readonly AppDbContext _dbContext;
    private readonly IAppClock _clock;

    public ReportService(AppDbContext dbContext, IAppClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<ReportSummaryDto> GetSummaryAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var scope = Scope(filter);

        var total = await scope.CountAsync(cancellationToken);
        var pending = await scope.CountAsync(
            task => task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled,
            cancellationToken);
        var completed = await scope.CountAsync(task => task.Status == TaskStatus.Completed, cancellationToken);
        var overdue = await scope.CountAsync(
            task => task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled && task.Deadline < now,
            cancellationToken);

        return new ReportSummaryDto(total, pending, completed, overdue);
    }

    public async Task<IReadOnlyCollection<ReportRowDto>> GetRowsAsync(
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        return await Scope(filter)
            .OrderBy(task => task.Deadline)
            .ThenBy(task => task.Id)
            .Select(task => new ReportRowDto(
                task.TaskCode,
                task.Title,
                task.AssignedTo == null ? string.Empty : task.AssignedTo.FullName,
                task.AssignedTo == null || task.AssignedTo.Department == null
                    ? string.Empty
                    : task.AssignedTo.Department.Name,
                task.Priority,
                task.Status,
                task.StartDate,
                task.Deadline,
                task.EstimatedHours,
                task.CompletedAt))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<TaskEntity> Scope(ReportFilter filter)
    {
        var tasks = _dbContext.Tasks.Where(task => !task.IsDeleted);

        if (filter.DeadlineFrom is not null)
        {
            tasks = tasks.Where(task => task.Deadline >= filter.DeadlineFrom);
        }

        if (filter.DeadlineTo is not null)
        {
            tasks = tasks.Where(task => task.Deadline <= filter.DeadlineTo);
        }

        if (filter.AssignedToUserId is not null)
        {
            tasks = tasks.Where(task => task.AssignedToUserId == filter.AssignedToUserId);
        }

        if (filter.DepartmentId is not null)
        {
            tasks = tasks.Where(task => task.AssignedTo != null && task.AssignedTo.DepartmentId == filter.DepartmentId);
        }

        if (filter.Status is not null)
        {
            tasks = tasks.Where(task => task.Status == filter.Status);
        }

        if (filter.Priority is not null)
        {
            tasks = tasks.Where(task => task.Priority == filter.Priority);
        }

        return tasks;
    }
}
