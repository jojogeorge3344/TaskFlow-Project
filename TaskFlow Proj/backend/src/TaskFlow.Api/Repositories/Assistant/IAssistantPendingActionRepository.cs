using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Repositories.Assistant;

/// <summary>Actions TFA prepared for a user to confirm.</summary>
public interface IAssistantPendingActionRepository : IBaseRepository<AssistantPendingAction>
{
    /// <summary>The user's actions among these ids.</summary>
    Task<IReadOnlyList<AssistantPendingAction>> GetForUserAsync(int userId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>The user's action with this id, untracked.</summary>
    Task<AssistantPendingAction?> FindForUserAsync(Guid id, int userId, CancellationToken cancellationToken = default);

    /// <summary>The user's tracked action with this id.</summary>
    Task<AssistantPendingAction?> FindForUserForUpdateAsync(Guid id, int userId, CancellationToken cancellationToken = default);

    /// <summary>Moves the action from Pending to Confirmed in one statement, so of several simultaneous confirms only one
    /// succeeds. Returns false when it was no longer pending.</summary>
    Task<bool> TryClaimAsync(Guid id, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Sets the action's status and resolution time in one statement.</summary>
    System.Threading.Tasks.Task SetStatusAsync(Guid id, AssistantActionStatus status, DateTime now, CancellationToken cancellationToken = default);
}
