using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Interfaces.Management.EmployeeManagement;

/// <summary>User accounts. Deleted accounts are hidden by the default query filter unless a method says otherwise.</summary>
public interface IEmployeeRepository : IBaseRepository<User>
{
    /// <summary>The live accounts matching the filter, by name, with their projects, departments, managers and reports.</summary>
    Task<IReadOnlyList<UserDto>> GetListAsync(UserListFilter? filter, CancellationToken cancellationToken = default);

    /// <summary>One live account as <see cref="GetListAsync"/> describes it, or null.</summary>
    Task<UserDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The tracked live account with its role, for changing it.</summary>
    Task<User?> GetActiveWithRoleForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The tracked account (hidden if deleted), for changing it.</summary>
    Task<User?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>How many accounts hold the role, deleted ones included.</summary>
    Task<int> CountHoldingRoleAsync(int roleId, CancellationToken cancellationToken = default);

    /// <summary>The tracked accounts holding the role, deleted ones included.</summary>
    Task<IReadOnlyList<User>> GetHoldingRoleForUpdateAsync(int roleId, CancellationToken cancellationToken = default);

    /// <summary>The live accounts with these ids, keyed by id. Unknown or deleted ids are absent.</summary>
    Task<IReadOnlyDictionary<int, User>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    /// <summary>The live, active accounts with these ids.</summary>
    Task<IReadOnlyList<User>> GetActiveByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Whether any account other than <paramref name="exceptUserId"/>, deleted ones included, has an email that
    /// starts with <paramref name="emailPrefix"/> (the local part and the "@").</summary>
    Task<bool> EmailPrefixTakenAsync(string emailPrefix, int? exceptUserId, CancellationToken cancellationToken = default);

    /// <summary>Whether any account other than <paramref name="exceptUserId"/>, deleted ones included, has this username,
    /// ignoring case.</summary>
    Task<bool> UsernameTakenAsync(string username, int? exceptUserId, CancellationToken cancellationToken = default);

    /// <summary>Whether another live account holds exactly this email and mobile number together.</summary>
    Task<bool> EmailAndMobileTakenAsync(string email, string mobileNumber, int exceptUserId, CancellationToken cancellationToken = default);

    /// <summary>How many live, active Administrators there are besides this account.</summary>
    Task<int> CountOtherActiveAdministratorsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Stages the removal of a manager's project and department oversight: their manager links go and their
    /// project-manager flags are cleared.</summary>
    System.Threading.Tasks.Task ClearManagerDutiesAsync(int managerUserId, CancellationToken cancellationToken = default);

    /// <summary>The tracked department memberships of the account.</summary>
    Task<IReadOnlyList<UserDepartment>> GetDepartmentMembershipsForUpdateAsync(int userId, CancellationToken cancellationToken = default);

    void RemoveDepartmentMemberships(IEnumerable<UserDepartment> memberships);
}
