using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Application.Settings;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Rules;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Analytics;
using TaskFlow.Infrastructure.Persistence;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Reads and mutates tasks, enforcing the task access scope, the status transition
/// table (TDD §10.2) and the edit/reassign business rules (§10.4-§10.5).</summary>
public class TaskService : ITaskService
{
    private readonly AppDbContext _dbContext;
    private readonly ITaskActivityService _activityService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly IReminderSettingsService _reminderSettings;
    private readonly IAppClock _clock;

    public TaskService(
        AppDbContext dbContext,
        ITaskActivityService activityService,
        INotificationPublisher notificationPublisher,
        TimeProvider timeProvider,
        IReminderSettingsService reminderSettings,
        IAppClock clock)
    {
        _dbContext = dbContext;
        _activityService = activityService;
        _notificationPublisher = notificationPublisher;
        _timeProvider = timeProvider;
        _reminderSettings = reminderSettings;
        _clock = clock;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>Whether the named role holds the permission, read from the database so changes apply immediately.</summary>
    private Task<bool> HasPermissionAsync(string roleName, string permissionKey, CancellationToken cancellationToken) =>
        _dbContext.RolePermissions
            .AsNoTracking()
            .AnyAsync(grant => grant.Role.Name == roleName && grant.Permission.Key == permissionKey, cancellationToken);

    private Task<bool> CanViewAllAsync(string roleName, CancellationToken cancellationToken) =>
        HasPermissionAsync(roleName, PermissionKeys.TasksViewAll, cancellationToken);

    public async Task<TaskDetailsDto?> GetDetailsAsync(
        int taskId,
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        var task = await LoadTaskAsync(taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        var recipients = await LoadRecipientsAsync(taskId, cancellationToken);
        if (!TaskAccessScope.CanAccess(currentUserId, await CanViewAllAsync(currentUserRole, cancellationToken), task, recipientIds(recipients)))
        {
            return null;
        }

        return ToDetails(task, recipients, await _clock.GetTimeZoneAsync(cancellationToken));
    }

    public async Task<IReadOnlyCollection<TaskCardDto>> GetBoardAsync(
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        var now = Now;
        var tasks = await ScopedTasks(currentUserId, await CanViewAllAsync(currentUserRole, cancellationToken))
            .IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Include(task => task.AssignedTo)
            .OrderBy(task => task.Deadline)
            .ToListAsync(cancellationToken);

        return tasks.Select(task => ToCard(task, now)).ToList();
    }

    public async Task<PagedResult<TaskListItemDto>> ListAsync(
        TaskListFilter filter,
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

        var query = ScopedTasks(currentUserId, await CanViewAllAsync(currentUserRole, cancellationToken));

        if (filter.AssignedToUserId is int assignedTo)
        {
            query = query.Where(task => task.AssignedToUserId == assignedTo);
        }

        if (filter.Status is TaskStatus status)
        {
            query = query.Where(task => task.Status == status);
        }

        if (filter.Priority is TaskPriority priority)
        {
            query = query.Where(task => task.Priority == priority);
        }

        if (filter.DeadlineFrom is DateTime deadlineFrom)
        {
            query = query.Where(task => task.Deadline >= deadlineFrom);
        }

        if (filter.DeadlineTo is DateTime deadlineTo)
        {
            query = query.Where(task => task.Deadline <= deadlineTo);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Escape LIKE wildcards so "%" and "_" are matched literally.
            var term = filter.Search.Trim()
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");
            query = query.Where(task =>
                EF.Functions.Like(task.Title, $"%{term}%", @"\")
                || EF.Functions.Like(task.TaskCode, $"%{term}%", @"\"));
        }

        query = await ApplyAnalyticsFiltersAsync(query, filter, cancellationToken);

        var total = await query.CountAsync(cancellationToken);

        var now = Now;
        var tasks = await query
            .IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Include(task => task.AssignedTo)
            .Include(task => task.CreatedBy)
            .OrderBy(task => task.Deadline)
            .ThenBy(task => task.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
        var items = tasks.Select(task => ToListItem(task, now, timeZone)).ToList();
        return new PagedResult<TaskListItemDto>(items, total, page, pageSize);
    }

    /// <summary>The dashboard drill-down filters. They reuse the analytics rules, so a list opened from a chart
    /// segment holds exactly the tasks the segment counted.</summary>
    private async Task<IQueryable<TaskEntity>> ApplyAnalyticsFiltersAsync(
        IQueryable<TaskEntity> query,
        TaskListFilter filter,
        CancellationToken cancellationToken)
    {
        var now = Now;

        if (filter.Bucket is TaskBucketFilter bucket)
        {
            query = bucket == TaskBucketFilter.All
                ? query.Where(TaskBucketRules.IsCounted())
                : query.Where(TaskBucketRules.InBucket(ToBucket(bucket), now));
        }

        if (filter.DeadlineBucket is DeadlineBucket deadlineBucket)
        {
            var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
            query = query.Where(DeadlineBucketRules.InBucket(deadlineBucket, DeadlineBucketRules.BoundsFor(now, timeZone)));
        }

        if (filter.From is not null || filter.To is not null)
        {
            if (filter.From is DateOnly start && filter.To is DateOnly end && start > end)
            {
                throw new AnalyticsFilterException("The end date cannot be before the start date.");
            }

            var timeZone = await _clock.GetTimeZoneAsync(cancellationToken);
            var fromUtc = filter.From is DateOnly from
                ? ReminderCalculator.LocalMidnightUtc(from, timeZone)
                : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            var toUtc = filter.To is DateOnly to
                ? ReminderCalculator.LocalMidnightUtc(to.AddDays(1), timeZone)
                : DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
            query = query.ApplyDateRange(filter.Basis, fromUtc, toUtc);
        }

        return query.ApplyEntityFilters(_dbContext, filter.DepartmentId, filter.ProjectId, filter.EmployeeId, filter.ManagerId);
    }

    private static TaskBucket ToBucket(TaskBucketFilter bucket) => bucket switch
    {
        TaskBucketFilter.Pending => TaskBucket.Pending,
        TaskBucketFilter.InProgress => TaskBucket.InProgress,
        TaskBucketFilter.Completed => TaskBucket.Completed,
        _ => TaskBucket.Overdue
    };

    public async Task<IReadOnlyCollection<TaskRecipientDto>> GetAssigneesAsync(
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        var assignees = await ScopedTasks(currentUserId, await CanViewAllAsync(currentUserRole, cancellationToken))
            .IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Select(task => new { task.AssignedToUserId, task.AssignedTo!.FullName })
            .Distinct()
            .ToListAsync(cancellationToken);

        return assignees
            .OrderBy(assignee => assignee.FullName)
            .Select(assignee => new TaskRecipientDto(assignee.AssignedToUserId, assignee.FullName))
            .ToList();
    }

    /// <summary>Non-deleted tasks the caller may access: everything for roles holding tasks.view.all,
    /// otherwise tasks they are assigned to, created, or receive notifications for.</summary>
    private IQueryable<TaskEntity> ScopedTasks(int currentUserId, bool canViewAll)
    {
        IQueryable<TaskEntity> query = _dbContext.Tasks.AsNoTracking().Where(task => !task.IsDeleted);

        if (!canViewAll)
        {
            var recipientTaskIds = _dbContext.TaskRecipients
                .Where(recipient => recipient.UserId == currentUserId)
                .Select(recipient => recipient.TaskId);

            query = query.Where(task =>
                task.AssignedToUserId == currentUserId
                || task.CreatedByUserId == currentUserId
                || recipientTaskIds.Contains(task.Id));
        }

        return query;
    }

    public async Task<TaskDetailsDto> CreateAsync(
        CreateTaskRequest request,
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(currentUserRole, PermissionKeys.TasksCreate, cancellationToken))
        {
            throw new UnauthorizedAccessException("Your role is not allowed to create tasks.");
        }

        ValidateCreate(request);

        var assignee = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == request.AssignedToUserId && !user.IsDeleted, cancellationToken);

        if (assignee is null || !assignee.IsActive)
        {
            throw new InvalidOperationException("The assigned employee does not exist or is not active.");
        }

        var additionalRecipientIds = request.AdditionalRecipientUserIds?.Distinct().ToList() ?? [];
        if (additionalRecipientIds.Count > 0)
        {
            var existing = await _dbContext.Users
                .Where(user => additionalRecipientIds.Contains(user.Id) && !user.IsDeleted)
                .CountAsync(cancellationToken);

            if (existing != additionalRecipientIds.Count)
            {
                throw new InvalidOperationException("One or more recipients do not exist.");
            }
        }

        // Omitted reminder fields take the configured defaults; existing tasks keep what they stored.
        var reminderDefaults = request.FirstReminderDays is null
            || request.SecondReminderDays is null
            || request.OverdueReminderFrequency is null
                ? await _reminderSettings.GetAsync(cancellationToken)
                : null;

        var firstReminderDays = request.FirstReminderDays ?? reminderDefaults!.DefaultFirstDays;
        var secondReminderDays = request.SecondReminderDays ?? reminderDefaults!.DefaultSecondDays;
        if (!ReminderIntervals.AreOrdered(firstReminderDays, secondReminderDays))
        {
            throw new InvalidOperationException(ReminderIntervals.OrderMessage);
        }

        var now = Now;
        var task = new TaskEntity
        {
            // A unique placeholder keeps the TaskCode index valid until the identity Id is known.
            // "P-" + 30 hex chars stays within the 32-character TaskCode column.
            TaskCode = $"P-{Guid.NewGuid():N}"[..32],
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            AssignedToUserId = assignee.Id,
            CreatedByUserId = currentUserId,
            Priority = request.Priority,
            StartDate = request.StartDate,
            Deadline = request.Deadline,
            EstimatedHours = request.EstimatedHours,
            Status = TaskStatus.ToDo,
            FirstReminderDays = firstReminderDays,
            SecondReminderDays = secondReminderDays,
            OverdueReminderFrequency = request.OverdueReminderFrequency ?? reminderDefaults!.DefaultOverdueFrequency,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.Tasks.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        task.TaskCode = $"TSK-{task.Id:D6}";

        // BR-5: the assignee is always a recipient, and recipients are unique per task.
        var recipientIds = new HashSet<int> { assignee.Id };
        recipientIds.UnionWith(additionalRecipientIds);
        foreach (var userId in recipientIds)
        {
            _dbContext.TaskRecipients.Add(new TaskFlow.Domain.Entities.TaskRecipient
            {
                TaskId = task.Id,
                UserId = userId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _activityService.AppendAsync(
            task.Id,
            TaskActivityEventType.Created,
            currentUserId,
            "Task created",
            newValue: task.Title,
            cancellationToken: cancellationToken);

        await _activityService.AppendAsync(
            task.Id,
            TaskActivityEventType.Assigned,
            currentUserId,
            $"Assigned to {assignee.FullName}",
            newValue: assignee.Id.ToString(),
            cancellationToken: cancellationToken);

        // Inside the transaction (design D5): if the notification fails, no task is left behind
        // for a retry to duplicate.
        await _notificationPublisher.PublishAsync(
            assignee.Id,
            task.Id,
            NotificationType.TaskAssigned,
            $"A task was assigned to you: {task.Title}",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var created = await LoadTaskAsync(task.Id, cancellationToken) ?? task;
        var createdRecipients = await LoadRecipientsAsync(task.Id, cancellationToken);
        return ToDetails(created, createdRecipients, await _clock.GetTimeZoneAsync(cancellationToken));
    }

    public async Task<TaskDetailsDto?> UpdateAsync(
        int taskId,
        UpdateTaskRequest request,
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(currentUserRole, PermissionKeys.TasksUpdate, cancellationToken))
        {
            throw new UnauthorizedAccessException("Your role is not allowed to edit tasks.");
        }

        ValidateUpdate(request);

        var task = await LoadTaskAsync(taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        var recipients = await LoadRecipientsAsync(taskId, cancellationToken);
        if (!TaskAccessScope.CanAccess(currentUserId, await CanViewAllAsync(currentUserRole, cancellationToken), task, recipientIds(recipients)))
        {
            throw new UnauthorizedAccessException("You do not have access to this task.");
        }

        if (task.Deadline != request.Deadline)
        {
            await _activityService.AppendAsync(
                taskId,
                TaskActivityEventType.DeadlineChanged,
                currentUserId,
                "Deadline updated",
                task.Deadline.ToString("O"),
                request.Deadline.ToString("O"),
                cancellationToken);
        }

        if (task.Priority != request.Priority)
        {
            await _activityService.AppendAsync(
                taskId,
                TaskActivityEventType.PriorityChanged,
                currentUserId,
                "Priority updated",
                task.Priority.ToString(),
                request.Priority.ToString(),
                cancellationToken);
        }

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Priority = request.Priority;
        task.StartDate = request.StartDate;
        task.Deadline = request.Deadline;
        task.EstimatedHours = request.EstimatedHours;
        task.UpdatedAt = Now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDetails(task, recipients, await _clock.GetTimeZoneAsync(cancellationToken));
    }

    public async Task<TaskCardDto?> ChangeStatusAsync(
        int taskId,
        UpdateTaskStatusRequest request,
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        var task = await LoadTaskAsync(taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        var recipients = await LoadRecipientsAsync(taskId, cancellationToken);
        if (!TaskAccessScope.CanAccess(currentUserId, await CanViewAllAsync(currentUserRole, cancellationToken), task, recipientIds(recipients)))
        {
            throw new UnauthorizedAccessException("You do not have access to this task.");
        }

        if (!TaskStatusTransitions.Can(task.Status, request.Status))
        {
            throw new InvalidOperationException($"Cannot move a task from {task.Status} to {request.Status}.");
        }

        var oldStatus = task.Status;
        var now = Now;

        task.Status = request.Status;
        task.UpdatedAt = now;
        if (request.Status == TaskStatus.Completed)
        {
            task.CompletedAt = now;
        }

        if (request.Status == TaskStatus.Cancelled)
        {
            task.CancelledAt = now;
        }

        await _activityService.AppendAsync(
            taskId,
            TaskActivityEventType.StatusChanged,
            currentUserId,
            $"Status changed from {oldStatus} to {request.Status}",
            oldStatus.ToString(),
            request.Status.ToString(),
            cancellationToken);

        if (request.Status == TaskStatus.Completed)
        {
            await _activityService.AppendAsync(
                taskId,
                TaskActivityEventType.Completed,
                currentUserId,
                "Task completed",
                cancellationToken: cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToCard(task, Now);
    }

    public async Task<TaskDetailsDto?> ReassignAsync(
        int taskId,
        ReassignTaskRequest request,
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(currentUserRole, PermissionKeys.TasksReassign, cancellationToken))
        {
            throw new UnauthorizedAccessException("Your role is not allowed to reassign tasks.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("A reassignment reason is required.");
        }

        var task = await LoadTaskAsync(taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        var recipients = await LoadRecipientsAsync(taskId, cancellationToken);
        if (!TaskAccessScope.CanAccess(currentUserId, await CanViewAllAsync(currentUserRole, cancellationToken), task, recipientIds(recipients)))
        {
            throw new UnauthorizedAccessException("You do not have access to this task.");
        }

        var newAssignee = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == request.NewAssigneeUserId && !user.IsDeleted, cancellationToken);

        if (newAssignee is null || !newAssignee.IsActive)
        {
            throw new InvalidOperationException("The target employee does not exist or is not active.");
        }

        var oldAssignee = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == task.AssignedToUserId, cancellationToken);

        var oldAssigneeId = task.AssignedToUserId;
        task.AssignedToUserId = request.NewAssigneeUserId;
        task.UpdatedAt = Now;

        await _activityService.AppendAsync(
            taskId,
            TaskActivityEventType.Reassigned,
            currentUserId,
            $"Reassigned from {oldAssignee?.FullName ?? "Unknown"} to {newAssignee.FullName}. Reason: {request.Reason}",
            oldAssigneeId.ToString(),
            request.NewAssigneeUserId.ToString(),
            cancellationToken);

        // BR-5: the assignee is always a recipient - make sure the new assignee is included.
        if (recipients.All(recipient => recipient.UserId != request.NewAssigneeUserId))
        {
            _dbContext.TaskRecipients.Add(new TaskFlow.Domain.Entities.TaskRecipient
            {
                TaskId = taskId,
                UserId = request.NewAssigneeUserId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationPublisher.PublishAsync(
            request.NewAssigneeUserId,
            taskId,
            NotificationType.Reassigned,
            $"A task was reassigned to you: {task.Title}",
            cancellationToken);

        var updatedRecipients = await LoadRecipientsAsync(taskId, cancellationToken);
        return ToDetails(task, updatedRecipients, await _clock.GetTimeZoneAsync(cancellationToken));
    }

    public async Task<IReadOnlyCollection<TaskRecipientDto>?> GetRecipientsAsync(
        int taskId,
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        var task = await LoadTaskAsync(taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        var recipients = await LoadRecipientsAsync(taskId, cancellationToken);
        if (!TaskAccessScope.CanAccess(currentUserId, await CanViewAllAsync(currentUserRole, cancellationToken), task, recipientIds(recipients)))
        {
            return null;
        }

        return recipients;
    }

    public async Task<bool> SetRecipientsAsync(
        int taskId,
        SetRecipientsRequest request,
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(currentUserRole, PermissionKeys.TasksRecipients, cancellationToken))
        {
            throw new UnauthorizedAccessException("Your role is not allowed to manage task recipients.");
        }

        var task = await LoadTaskAsync(taskId, cancellationToken);
        if (task is null)
        {
            return false;
        }

        var currentRecipients = await _dbContext.TaskRecipients
            .Where(recipient => recipient.TaskId == taskId)
            .ToListAsync(cancellationToken);

        var desired = new HashSet<int>(request.RecipientUserIds) { task.AssignedToUserId };

        var toAdd = desired.Except(currentRecipients.Select(recipient => recipient.UserId)).ToList();
        var toRemove = currentRecipients
            .Where(recipient => !desired.Contains(recipient.UserId) && recipient.UserId != task.AssignedToUserId)
            .ToList();

        foreach (var userId in toAdd)
        {
            _dbContext.TaskRecipients.Add(new TaskFlow.Domain.Entities.TaskRecipient
            {
                TaskId = taskId,
                UserId = userId
            });
        }

        if (toRemove.Count > 0)
        {
            _dbContext.TaskRecipients.RemoveRange(toRemove);
        }

        task.UpdatedAt = Now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _activityService.AppendAsync(
            taskId,
            TaskActivityEventType.Assigned,
            currentUserId,
            "Task recipients updated",
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(int taskId, int currentUserId, CancellationToken cancellationToken)
    {
        var task = await _dbContext.Tasks
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId && !candidate.IsDeleted, cancellationToken);
        if (task is null)
        {
            return false;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        task.IsDeleted = true;
        task.UpdatedAt = Now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Comments and earlier history stay; this entry says who removed the task and when.
        await _activityService.AppendAsync(
            task.Id,
            TaskActivityEventType.Deleted,
            currentUserId,
            "Task deleted",
            oldValue: task.Title,
            cancellationToken: cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static IReadOnlyCollection<int> recipientIds(IReadOnlyCollection<TaskRecipientDto> recipients) =>
        recipients.Select(recipient => recipient.UserId).ToList();

    private async Task<TaskEntity?> LoadTaskAsync(int taskId, CancellationToken cancellationToken) =>
        await _dbContext.Tasks
            .IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Include(task => task.AssignedTo)
            .Include(task => task.CreatedBy)
            .FirstOrDefaultAsync(task => task.Id == taskId && !task.IsDeleted, cancellationToken);

    private async Task<IReadOnlyCollection<TaskRecipientDto>> LoadRecipientsAsync(
        int taskId,
        CancellationToken cancellationToken) =>
        await _dbContext.TaskRecipients
            .AsNoTracking()
            .Include(recipient => recipient.User)
            .Where(recipient => recipient.TaskId == taskId)
            .OrderBy(recipient => recipient.UserId)
            .Select(recipient => new TaskRecipientDto(
                recipient.UserId,
                recipient.User != null ? recipient.User.FullName : string.Empty))
            .ToListAsync(cancellationToken);

    private static void ValidateUpdate(UpdateTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("Title is required.");
        }

        if (request.Deadline <= request.StartDate)
        {
            throw new InvalidOperationException("Deadline must be after the start date.");
        }

        if (request.EstimatedHours <= 0)
        {
            throw new InvalidOperationException("Estimated hours must be a positive number.");
        }
    }

    private static void ValidateCreate(CreateTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("Title is required.");
        }

        if (request.AssignedToUserId <= 0)
        {
            throw new InvalidOperationException("An assigned employee is required.");
        }

        if (request.Deadline <= request.StartDate)
        {
            throw new InvalidOperationException("Deadline must be after the start date.");
        }

        if (request.EstimatedHours <= 0)
        {
            throw new InvalidOperationException("Estimated hours must be a positive number.");
        }
    }

    private TaskDetailsDto ToDetails(TaskEntity task, IReadOnlyCollection<TaskRecipientDto> recipients, TimeZoneInfo timeZone)
    {
        var stage = TaskStageCalculator.Calculate(task.Status, task.Deadline, Now, timeZone).ToString();

        return new TaskDetailsDto(
            task.Id,
            task.TaskCode,
            task.Title,
            task.Description,
            task.AssignedToUserId,
            task.AssignedTo?.FullName ?? string.Empty,
            task.CreatedByUserId,
            task.CreatedBy?.FullName ?? string.Empty,
            task.Priority,
            task.StartDate,
            task.Deadline,
            task.EstimatedHours,
            task.Status,
            stage,
            recipients,
            task.CreatedAt,
            task.UpdatedAt);
    }

    private TaskCardDto ToCard(TaskEntity task, DateTime utcNow)
    {
        var isOverdue = task.Deadline < utcNow
            && task.Status != TaskStatus.Completed
            && task.Status != TaskStatus.Cancelled;

        return new TaskCardDto(
            task.Id,
            task.TaskCode,
            task.Title,
            task.AssignedToUserId,
            task.AssignedTo?.FullName ?? string.Empty,
            task.Priority,
            task.Deadline,
            task.Status,
            isOverdue);
    }

    private TaskListItemDto ToListItem(TaskEntity task, DateTime utcNow, TimeZoneInfo timeZone)
    {
        var stage = TaskStageCalculator.Calculate(task.Status, task.Deadline, utcNow, timeZone);

        return new TaskListItemDto(
            task.Id,
            task.TaskCode,
            task.Title,
            task.AssignedToUserId,
            task.AssignedTo?.FullName ?? string.Empty,
            task.CreatedByUserId,
            task.CreatedBy?.FullName ?? string.Empty,
            task.Priority,
            task.StartDate,
            task.Deadline,
            task.EstimatedHours,
            task.Status,
            stage.ToString(),
            stage == TaskStage.Overdue);
    }
}