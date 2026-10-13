using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Rules;
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

    public TaskService(
        AppDbContext dbContext,
        ITaskActivityService activityService,
        INotificationPublisher notificationPublisher,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _activityService = activityService;
        _notificationPublisher = notificationPublisher;
        _timeProvider = timeProvider;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    private static bool IsManagerOrAdmin(string role) =>
        role is nameof(Role.Manager) or nameof(Role.Administrator);

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
        if (!TaskAccessScope.CanAccess(currentUserId, currentUserRole, task, recipientIds(recipients)))
        {
            return null;
        }

        return ToDetails(task, recipients);
    }

    public async Task<IReadOnlyCollection<TaskCardDto>> GetBoardAsync(
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        IQueryable<TaskEntity> query = _dbContext.Tasks.AsNoTracking().Where(task => !task.IsDeleted);

        if (!IsManagerOrAdmin(currentUserRole))
        {
            var recipientTaskIds = _dbContext.TaskRecipients
                .Where(recipient => recipient.UserId == currentUserId)
                .Select(recipient => recipient.TaskId);

            query = query.Where(task =>
                task.AssignedToUserId == currentUserId
                || task.CreatedByUserId == currentUserId
                || recipientTaskIds.Contains(task.Id));
        }

        var now = Now;
        var tasks = await query
            .Include(task => task.AssignedTo)
            .OrderBy(task => task.Deadline)
            .ToListAsync(cancellationToken);

        return tasks.Select(task => ToCard(task, now)).ToList();
    }

    public async Task<TaskDetailsDto?> UpdateAsync(
        int taskId,
        UpdateTaskRequest request,
        int currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken)
    {
        if (!IsManagerOrAdmin(currentUserRole))
        {
            throw new UnauthorizedAccessException("Only Managers and Administrators can edit tasks.");
        }

        ValidateUpdate(request);

        var task = await LoadTaskAsync(taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        var recipients = await LoadRecipientsAsync(taskId, cancellationToken);
        if (!TaskAccessScope.CanAccess(currentUserId, currentUserRole, task, recipientIds(recipients)))
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

        return ToDetails(task, recipients);
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
        if (!TaskAccessScope.CanAccess(currentUserId, currentUserRole, task, recipientIds(recipients)))
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
        if (!IsManagerOrAdmin(currentUserRole))
        {
            throw new UnauthorizedAccessException("Only Managers and Administrators can reassign tasks.");
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
        if (!TaskAccessScope.CanAccess(currentUserId, currentUserRole, task, recipientIds(recipients)))
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
        return ToDetails(task, updatedRecipients);
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
        if (!TaskAccessScope.CanAccess(currentUserId, currentUserRole, task, recipientIds(recipients)))
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
        if (!IsManagerOrAdmin(currentUserRole))
        {
            throw new UnauthorizedAccessException("Only Managers and Administrators can manage task recipients.");
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

    private static IReadOnlyCollection<int> recipientIds(IReadOnlyCollection<TaskRecipientDto> recipients) =>
        recipients.Select(recipient => recipient.UserId).ToList();

    private async Task<TaskEntity?> LoadTaskAsync(int taskId, CancellationToken cancellationToken) =>
        await _dbContext.Tasks
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

    private TaskDetailsDto ToDetails(TaskEntity task, IReadOnlyCollection<TaskRecipientDto> recipients)
    {
        var stage = TaskStageCalculator.Calculate(task.Status, task.Deadline, Now).ToString();

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
}