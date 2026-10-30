using TaskFlow.Api.Services.Interfaces.Settings.ApplicationSettings;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Reports;
using TaskFlow.Api.Persistence;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Filtered task report aggregation and CSV row projection (Screen 17). Organization-wide
/// for a Manager/Administrator, matching the dashboards' scoping (see the change design, D3), with the
/// shared status and overdue rules (D7). Project is the assignee's project.</summary>
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
        var rows = await Scope(filter)
            .OrderBy(task => task.Deadline)
            .ThenBy(task => task.Id)
            .Take(MaxExportRows)
            .Select(task => new
            {
                task.TaskCode,
                task.Title,
                Assignee = task.AssignedTo == null ? string.Empty : task.AssignedTo.FullName,
                task.AssignedToUserId,
                task.Priority,
                task.Status,
                task.StartDate,
                task.Deadline,
                task.EstimatedHours,
                task.CompletedAt
            })
            .ToListAsync(cancellationToken);

        // The Project column lists every project the assignee is on. Filters are ignored so a deleted person's work keeps its projects.
        var assigneeIds = rows.Select(row => row.AssignedToUserId).Distinct().ToList();
        var projectNames = (await _dbContext.UserProjectAllocations
                .IgnoreQueryFilters()
                .Where(allocation => assigneeIds.Contains(allocation.UserId))
                .OrderBy(allocation => allocation.Project.Name)
                .Select(allocation => new { allocation.UserId, allocation.Project.Name })
                .ToListAsync(cancellationToken))
            .GroupBy(allocation => allocation.UserId)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(allocation => allocation.Name)));

        return rows
            .Select(row => new ReportRowDto(
                row.TaskCode,
                row.Title,
                row.Assignee,
                projectNames.GetValueOrDefault(row.AssignedToUserId, string.Empty),
                row.Priority,
                row.Status,
                row.StartDate,
                row.Deadline,
                row.EstimatedHours,
                row.CompletedAt))
            .ToList();
    }

    private IQueryable<TaskEntity> Scope(ReportFilter filter)
    {
        // Users are soft-deleted behind a query filter; reports must keep a deleted person's finished work and name.
        var tasks = _dbContext.Tasks.IgnoreQueryFilters().Where(task => !task.IsDeleted);

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

        if (filter.ProjectId is not null)
        {
            tasks = tasks.Where(task => task.AssignedTo != null
                && task.AssignedTo.ProjectAllocations.Any(allocation => allocation.ProjectId == filter.ProjectId));
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