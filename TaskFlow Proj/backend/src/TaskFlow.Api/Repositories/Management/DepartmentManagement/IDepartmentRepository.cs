using TaskFlow.Api.Dtos.Management.DepartmentManagement;
using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Management.DepartmentManagement;

/// <summary>Departments with their manager links and employee memberships. Links left behind by a deleted user never
/// count, because users have a soft-delete filter.</summary>
public interface IDepartmentRepository : IBaseRepository<Department>
{
    /// <summary>Every department with how many managers and employees it has.</summary>
    Task<IReadOnlyList<DepartmentWithCountsDto>> GetAllWithCountsAsync(CancellationToken cancellationToken = default);

    Task<Department?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The tracked department, unless it has been deleted.</summary>
    Task<Department?> GetActiveForUpdateAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>How many of these ids are live departments.</summary>
    Task<int> CountActiveAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Whether another live department has this name, ignoring case.</summary>
    Task<bool> NameTakenAsync(string name, int? exceptDepartmentId, CancellationToken cancellationToken = default);

    /// <summary>Whether any live manager or any employee still belongs to the department.</summary>
    Task<bool> HasMembersAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberRefDto>> GetManagersAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberRefDto>> GetEmployeesAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The tracked manager links of the department, whatever the state of the manager's account.</summary>
    Task<IReadOnlyList<DepartmentManager>> GetManagerLinksForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The tracked employee memberships of the department.</summary>
    Task<IReadOnlyList<UserDepartment>> GetMembershipsForUpdateAsync(int id, CancellationToken cancellationToken = default);

    void AddManagerLink(DepartmentManager link);

    void RemoveManagerLinks(IEnumerable<DepartmentManager> links);

    void AddMembership(UserDepartment membership);

    void RemoveMemberships(IEnumerable<UserDepartment> memberships);
}
