using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common.Models;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Dtos.Monitoring.TaskManagement;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Dashboard.Analytics;

namespace TaskFlow.Api.Repositories.Monitoring.TaskManagement.Tasks;

public class TaskRepository : BaseRepository<TaskEntity>, ITaskRepository
{
    public TaskRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<PagedResult<TaskEntity>> ListAsync(TaskListCriteria criteria, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Visible(criteria.Visibility);

        if (criteria.AssignedToUserId is int assignedTo)
        {
            query = query.Where(task => task.AssignedToUserId == assignedTo);
        }

        if (criteria.Status is TaskStatus status)
        {
            query = query.Where(task => task.Status == status);
        }

        if (criteria.Priority is { } priority)
        {
            query = query.Where(task => task.Priority == priority);
        }

        if (criteria.DeadlineFrom is DateTime deadlineFrom)
        {
            query = query.Where(task => task.Deadline >= deadlineFrom);
        }

        if (criteria.DeadlineTo is DateTime deadlineTo)
        {
            query = query.Where(task => task.Deadline <= deadlineTo);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            // Escape LIKE wildcards so "%" and "_" are matched literally.
            var term = criteria.Search.Trim()
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");
            query = query.Where(task =>
                EF.Functions.Like(task.Title, $"%{term}%", @"\")
                || EF.Functions.Like(task.TaskCode, $"%{term}%", @"\"));
        }

        // The dashboard drill-down filters reuse the analytics rules, so a list opened from a chart segment holds exactly
        // the tasks the segment counted.
        if (criteria.Bucket is { } bucket)
        {
            query = bucket.All
                ? query.Where(TaskBucketRules.IsCounted())
                : query.Where(TaskBucketRules.InBucket(bucket.Bucket!.Value, bucket.Now));
        }

        if (criteria.DeadlineBucket is DeadlineBucket deadlineBucket && criteria.DeadlineBounds is DeadlineBounds bounds)
        {
            query = query.Where(DeadlineBucketRules.InBucket(deadlineBucket, bounds));
        }

        if (criteria.DateRange is { } range)
        {
            query = query.ApplyDateRange(range.Basis, range.FromUtc, range.ToUtcExclusive);
        }

        query = query.ApplyEntityFilters(Db, criteria.DepartmentId, criteria.ProjectId, criteria.EmployeeId, criteria.ManagerId);

        var total = await query.CountAsync(cancellationToken);
        var tasks = await query
            .IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Include(task => task.AssignedTo)
            .Include(task => task.CreatedBy)
            .Include(task => task.Project)
            .OrderBy(task => task.Deadline)
            .ThenBy(task => task.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskEntity>(tasks, total, page, pageSize);
    }

    public async Task<IReadOnlyList<TaskEntity>> GetBoardAsync(TaskVisibility visibility, CancellationToken cancellationToken = default) =>
        await Visible(visibility)
            .IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Include(task => task.AssignedTo)
            .Include(task => task.Project)
            .OrderBy(task => task.Deadline)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskRecipientDto>> GetAssigneesAsync(TaskVisibility visibility, CancellationToken cancellationToken = default)
    {
        var assignees = await Visible(visibility)
            .IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Select(task => new { task.AssignedToUserId, task.AssignedTo!.FullName })
            .Distinct()
            .ToListAsync(cancellationToken);

        return assignees
            .OrderBy(assignee => assignee.FullName)
            .Select(assignee => new TaskRecipientDto(assignee.AssignedToUserId, assignee.FullName))
            .ToList();
    }

    public Task<bool> IsTeamTaskAsync(int taskId, IReadOnlyList<int> teamUserIds, CancellationToken cancellationToken = default) =>
        TeamTasks(Set.Where(candidate => candidate.Id == taskId), teamUserIds).AnyAsync(cancellationToken);

    public Task<TaskEntity?> GetWithPeopleForUpdateAsync(int taskId, CancellationToken cancellationToken = default) =>
        Set.IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Include(task => task.AssignedTo)
            .Include(task => task.CreatedBy)
            .Include(task => task.Project)
            .FirstOrDefaultAsync(task => task.Id == taskId && !task.IsDeleted, cancellationToken);

    public Task<TaskEntity?> GetLiveForUpdateAsync(int taskId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(candidate => candidate.Id == taskId && !candidate.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<TaskRecipientDto>> GetRecipientsAsync(int taskId, CancellationToken cancellationToken = default) =>
        await Db.TaskRecipients
            .AsNoTracking()
            .Include(recipient => recipient.User)
            .Where(recipient => recipient.TaskId == taskId)
            .OrderBy(recipient => recipient.UserId)
            .Select(recipient => new TaskRecipientDto(
                recipient.UserId,
                recipient.User != null ? recipient.User.FullName : string.Empty))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskRecipient>> GetRecipientRowsForUpdateAsync(int taskId, CancellationToken cancellationToken = default) =>
        await Db.TaskRecipients.Where(recipient => recipient.TaskId == taskId).ToListAsync(cancellationToken);

    public Task<TaskEntity?> GetLiveAsync(int taskId, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(task => task.Id == taskId && !task.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<int>> GetRecipientIdsAsync(int taskId, CancellationToken cancellationToken = default) =>
        await Db.TaskRecipients.AsNoTracking().Where(recipient => recipient.TaskId == taskId).Select(recipient => recipient.UserId).ToListAsync(cancellationToken);

    public void AddRecipient(TaskRecipient recipient) => Db.TaskRecipients.Add(recipient);

    public void RemoveRecipients(IEnumerable<TaskRecipient> recipients) => Db.TaskRecipients.RemoveRange(recipients);

    public Task<int> CountUnfinishedAssignedToAsync(int userId, CancellationToken cancellationToken = default) =>
        Set.CountAsync(
            t => t.AssignedToUserId == userId
                 && !t.IsDeleted
                 && t.Status != TaskStatus.Completed
                 && t.Status != TaskStatus.Cancelled,
            cancellationToken);

    /// <summary>The live tasks the visibility allows.</summary>
    private IQueryable<TaskEntity> Visible(TaskVisibility visibility)
    {
        var query = Scoped(visibility.UserId, visibility.ViewAll);
        if (visibility.TeamUserIds is not { } team)
        {
            return query;
        }

        var teamTaskIds = TeamTasks(query, team).Select(task => task.Id);
        if (!visibility.OrOwn)
        {
            return query.Where(task => teamTaskIds.Contains(task.Id));
        }

        var ownTaskIds = Scoped(visibility.UserId, viewAll: false).Select(task => task.Id);
        return query.Where(task => teamTaskIds.Contains(task.Id) || ownTaskIds.Contains(task.Id));
    }

    /// <summary>Live tasks: every one with <paramref name="viewAll"/>, otherwise those the user is assigned to, created, or
    /// receives notifications for.</summary>
    private IQueryable<TaskEntity> Scoped(int userId, bool viewAll)
    {
        IQueryable<TaskEntity> query = Set.AsNoTracking().Where(task => !task.IsDeleted);
        if (!viewAll)
        {
            var recipientTaskIds = Db.TaskRecipients
                .Where(recipient => recipient.UserId == userId)
                .Select(recipient => recipient.TaskId);

            query = query.Where(task =>
                task.AssignedToUserId == userId
                || task.CreatedByUserId == userId
                || recipientTaskIds.Contains(task.Id));
        }

        return query;
    }

    private IQueryable<TaskEntity> TeamTasks(IQueryable<TaskEntity> tasks, IReadOnlyList<int> team) =>
        tasks.Where(task =>
            team.Contains(task.AssignedToUserId)
            || Db.TaskRecipients.Any(recipient => recipient.TaskId == task.Id && team.Contains(recipient.UserId)));
}
