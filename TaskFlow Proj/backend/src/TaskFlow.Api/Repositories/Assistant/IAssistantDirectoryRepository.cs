using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Repositories.Assistant;

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
