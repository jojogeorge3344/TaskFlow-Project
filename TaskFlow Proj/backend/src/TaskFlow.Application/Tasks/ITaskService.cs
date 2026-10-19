namespace TaskFlow.Application.Tasks;

public interface ITaskService
{
    /// <summary>Returns task details for a caller with task access, or null when the task does
    /// not exist or is outside the caller's scope.</summary>
    Task<TaskDetailsDto?> GetDetailsAsync(int taskId, int currentUserId, string currentUserRole, CancellationToken cancellationToken);

    /// <summary>All tasks the caller may access, flat list for the Kanban board (grouped
    /// client-side).</summary>
    Task<IReadOnlyCollection<TaskCardDto>> GetBoardAsync(int currentUserId, string currentUserRole, CancellationToken cancellationToken);

    /// <summary>A page of tasks the caller may access, filtered and searched (Screen 7).
    /// Scope follows the same rule as the board.</summary>
    Task<PagedResult<TaskListItemDto>> ListAsync(TaskListFilter filter, int currentUserId, string currentUserRole, CancellationToken cancellationToken);

    /// <summary>The distinct employees assigned to tasks the caller may access, for the list's
    /// employee filter.</summary>
    Task<IReadOnlyCollection<TaskRecipientDto>> GetAssigneesAsync(int currentUserId, string currentUserRole, CancellationToken cancellationToken);

    /// <summary>Creates a task assigned to an employee (Screen 8). Manager/Administrator only;
    /// throws on unauthorized access or invalid input.</summary>
    Task<TaskDetailsDto> CreateAsync(CreateTaskRequest request, int currentUserId, string currentUserRole, CancellationToken cancellationToken);

    /// <summary>Edits an editable task (Manager/Administrator). Returns null when not found;
    /// throws when the caller lacks scope or the edit violates task rules.</summary>
    Task<TaskDetailsDto?> UpdateAsync(int taskId, UpdateTaskRequest request, int currentUserId, string currentUserRole, CancellationToken cancellationToken);

    /// <summary>Soft-deletes a task and records who did it in its activity history. Returns false when the task
    /// does not exist or is already deleted. Administrator-only; the caller enforces that.</summary>
    Task<bool> DeleteAsync(int taskId, int currentUserId, CancellationToken cancellationToken);

    /// <summary>Changes a task's status enforcing the allowed transitions. Returns null when
    /// not found; throws on unauthorized access or a disallowed transition.</summary>
    Task<TaskCardDto?> ChangeStatusAsync(int taskId, UpdateTaskStatusRequest request, int currentUserId, string currentUserRole, CancellationToken cancellationToken);

    /// <summary>Reassigns a task to another active employee. Returns null when not found;
    /// throws on unauthorized access or an invalid target assignee.</summary>
    Task<TaskDetailsDto?> ReassignAsync(int taskId, ReassignTaskRequest request, int currentUserId, string currentUserRole, CancellationToken cancellationToken);

    /// <summary>Recipients of a task, or null when the task does not exist or is outside the
    /// caller's scope.</summary>
    Task<IReadOnlyCollection<TaskRecipientDto>?> GetRecipientsAsync(int taskId, int currentUserId, string currentUserRole, CancellationToken cancellationToken);

    /// <summary>Replaces the additional recipient list (assignee always stays included).
    /// Throws on unauthorized access or a missing task; returns true on success.</summary>
    Task<bool> SetRecipientsAsync(int taskId, SetRecipientsRequest request, int currentUserId, string currentUserRole, CancellationToken cancellationToken);
}