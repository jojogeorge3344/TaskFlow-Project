using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Repositories.Interfaces.Assistant;

/// <summary>A user's TFA conversations; the latest one is the active one.</summary>
public interface IAssistantConversationRepository : IBaseRepository<AssistantConversation>
{
    /// <summary>The tracked latest conversation of the user, or null.</summary>
    Task<AssistantConversation?> FindLatestForUpdateAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>The stored messages of TFA conversations.</summary>
public interface IAssistantMessageRepository : IBaseRepository<AssistantMessage>
{
    /// <summary>The newest <paramref name="take"/> messages of the conversation, newest first.</summary>
    Task<IReadOnlyList<AssistantMessage>> GetNewestAsync(int conversationId, int take, CancellationToken cancellationToken = default);
}

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

/// <summary>The audit trail of TFA actions.</summary>
public interface IAssistantAuditRepository : IBaseRepository<AssistantAudit>
{
}

/// <summary>The people and roles TFA can name. Only active users are found; deleted users are hidden by the global filter.</summary>
public interface IAssistantDirectoryRepository : IBaseRepository<User>
{
    Task<string?> GetFullNameAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The user's current name and email.</summary>
    Task<(string FullName, string Email)?> GetNameAndEmailAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Whether another user already has this email, compared case-insensitively.</summary>
    Task<bool> EmailUsedByOtherAsync(string email, int exceptUserId, CancellationToken cancellationToken = default);

    /// <summary>The role with exactly this name, as (id, name).</summary>
    Task<(int Id, string Name)?> FindRoleByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<ResolvedUser?> FindActiveAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The active user with this email, compared case-insensitively.</summary>
    Task<ResolvedUser?> FindActiveByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Active users whose full name or username is exactly <paramref name="lowerName"/> ignoring case.</summary>
    Task<IReadOnlyList<ResolvedUser>> FindActiveByExactNameAsync(string lowerName, int take, CancellationToken cancellationToken = default);

    /// <summary>Active users whose full name or username contains <paramref name="lowerTerm"/>, by name.</summary>
    Task<IReadOnlyList<ResolvedUser>> SearchActiveByNameAsync(string lowerTerm, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetDepartmentIdsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Active Managers, other than <paramref name="exceptUserId"/>, who manage any of these departments, by name.</summary>
    Task<IReadOnlyList<ResolvedUser>> GetActiveManagersOfDepartmentsAsync(
        IReadOnlyCollection<int> departmentIds,
        int exceptUserId,
        CancellationToken cancellationToken = default);

    /// <summary>The active users with these lower-case addresses.</summary>
    Task<IReadOnlyList<MailRecipientRef>> GetActiveRecipientsAsync(IReadOnlyCollection<string> lowerEmails, CancellationToken cancellationToken = default);
}
