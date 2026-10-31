using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Common.Models;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Dtos.Monitoring.TaskManagement;
using TaskFlow.Api.Models.Entities;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Monitoring.TaskManagement.Tasks;

/// <summary>Which live tasks a caller may see, as decided by the task service. With <see cref="ViewAll"/> every live task
/// is a candidate, otherwise only the caller's own (assigned, created or receiving notifications). When
/// <see cref="TeamUserIds"/> is given the candidates are further limited to the team's tasks (assigned to, or notifying,
/// a team member), plus, with <see cref="OrOwn"/>, the caller's own.</summary>
public record TaskVisibility(int UserId, bool ViewAll, IReadOnlyList<int>? TeamUserIds = null, bool OrOwn = false);

/// <summary>A dashboard bucket to filter by: every counted task, or one bucket as of <see cref="Now"/>.</summary>
public record TaskBucketSelection(TaskBucket? Bucket, DateTime Now)
{
    public bool All => Bucket is null;
}

/// <summary>A dashboard date range on one of the task dates, end exclusive.</summary>
public record TaskDateRange(AnalyticsDateBasis Basis, DateTime FromUtc, DateTime ToUtcExclusive);

/// <summary>Everything a task list query can filter on. All given filters apply together.</summary>
public record TaskListCriteria(
    TaskVisibility Visibility,
    int? AssignedToUserId = null,
    TaskStatus? Status = null,
    TaskPriority? Priority = null,
    DateTime? DeadlineFrom = null,
    DateTime? DeadlineTo = null,
    string? Search = null,
    TaskBucketSelection? Bucket = null,
    DeadlineBucket? DeadlineBucket = null,
    DeadlineBounds? DeadlineBounds = null,
    TaskDateRange? DateRange = null,
    int? DepartmentId = null,
    int? ProjectId = null,
    int? EmployeeId = null,
    int? ManagerId = null);

/// <summary>Tasks and their notification recipients. People on a task are loaded even when their account is deleted, so
/// history keeps showing their names.</summary>
public interface ITaskRepository : IBaseRepository<TaskEntity>
{
    /// <summary>One page of matching tasks with assignee, creator and project, soonest deadline first.</summary>
    Task<PagedResult<TaskEntity>> ListAsync(TaskListCriteria criteria, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Every visible task with assignee and project, soonest deadline first.</summary>
    Task<IReadOnlyList<TaskEntity>> GetBoardAsync(TaskVisibility visibility, CancellationToken cancellationToken = default);

    /// <summary>The distinct assignees of visible tasks, by name.</summary>
    Task<IReadOnlyList<TaskRecipientDto>> GetAssigneesAsync(TaskVisibility visibility, CancellationToken cancellationToken = default);

    /// <summary>Whether the task is assigned to, or notifies, one of these people.</summary>
    Task<bool> IsTeamTaskAsync(int taskId, IReadOnlyList<int> teamUserIds, CancellationToken cancellationToken = default);

    /// <summary>The tracked live task with assignee, creator and project.</summary>
    Task<TaskEntity?> GetWithPeopleForUpdateAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>The tracked live task, without its people.</summary>
    Task<TaskEntity?> GetLiveForUpdateAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>The task's recipients with their names, by user id.</summary>
    Task<IReadOnlyList<TaskRecipientDto>> GetRecipientsAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>The task's tracked recipient rows, for replacing them.</summary>
    Task<IReadOnlyList<TaskRecipient>> GetRecipientRowsForUpdateAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>The live task, read-only.</summary>
    Task<TaskEntity?> GetLiveAsync(int taskId, CancellationToken cancellationToken = default);

    /// <summary>The user ids of the task's recipients.</summary>
    Task<IReadOnlyList<int>> GetRecipientIdsAsync(int taskId, CancellationToken cancellationToken = default);

    void AddRecipient(TaskRecipient recipient);

    void RemoveRecipients(IEnumerable<TaskRecipient> recipients);

    /// <summary>How many live tasks assigned to this person are neither completed nor cancelled.</summary>
    Task<int> CountUnfinishedAssignedToAsync(int userId, CancellationToken cancellationToken = default);
}
