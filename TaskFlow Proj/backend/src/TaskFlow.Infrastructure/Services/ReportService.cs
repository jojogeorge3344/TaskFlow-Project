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
    /// <summary>Upper bound on exported rows, so an unfiltered export cannot exhaust memory or
    /// produce an unbounded response.</summary>
    public const int MaxExportRows = 50_000;

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

        var summary = await Scope(filter)
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

    public async Task<IReadOnlyCollection<ReportRowDto>> GetRowsAsync(
        ReportFilter filter,
        CancellationToken cancellationToken)
    {
        return await Scope(filter)
            .OrderBy(task => task.Deadline)
            .ThenBy(task => task.Id)
            .Take(MaxExportRows)
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

        var deadlineFrom = AsUtc(filter.DeadlineFrom);
        if (deadlineFrom is not null)
        {
            tasks = tasks.Where(task => task.Deadline >= deadlineFrom);
        }

        var deadlineTo = AsUtc(filter.DeadlineTo);
        if (deadlineTo is not null)
        {
            tasks = tasks.Where(task => task.Deadline <= deadlineTo);
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

    /// <summary>Deadline bounds arrive as query-string values. A `Z`-suffixed instant binds to a
    /// local-kind <see cref="DateTime"/> that preserves the instant but shifts the wall clock, so the
    /// range would drift by the host's UTC offset. Normalise every bound to its UTC instant before
    /// filtering; a value with no offset is treated as UTC, matching how deadlines are stored.</summary>
    private static DateTime? AsUtc(DateTime? value) => value?.Kind switch
    {
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        _ => value
    };
}