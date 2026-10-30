using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Interfaces.Management.EmployeeManagement;

/// <summary>User accounts. Deleted accounts are hidden by the default query filter unless a method says otherwise.</summary>
public interface IEmployeeRepository : IBaseRepository<User>
{
    /// <summary>How many accounts hold the role, deleted ones included.</summary>
    Task<int> CountHoldingRoleAsync(int roleId, CancellationToken cancellationToken = default);

    /// <summary>The tracked accounts holding the role, deleted ones included.</summary>
    Task<IReadOnlyList<User>> GetHoldingRoleForUpdateAsync(int roleId, CancellationToken cancellationToken = default);
}
