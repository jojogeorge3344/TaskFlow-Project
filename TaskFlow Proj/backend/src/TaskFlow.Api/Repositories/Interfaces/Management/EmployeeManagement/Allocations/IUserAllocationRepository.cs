using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Interfaces.Management.EmployeeManagement.Allocations;

/// <summary>People's allocations to projects, and the "manager oversees this project" links kept in step with the
/// project-manager flags.</summary>
public interface IUserAllocationRepository : IBaseRepository<UserProjectAllocation>
{
    /// <summary>The tracked allocations of one person.</summary>
    Task<List<UserProjectAllocation>> GetForUserForUpdateAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The tracked allocations of one project's team.</summary>
    Task<List<UserProjectAllocation>> GetForProjectForUpdateAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>Each person's total percentage on every project except <paramref name="projectId"/>. People with none are
    /// absent.</summary>
    Task<IReadOnlyDictionary<int, int>> GetTotalsOnOtherProjectsAsync(
        IReadOnlyCollection<int> userIds,
        int projectId,
        CancellationToken cancellationToken = default);

    void RemoveRange(IEnumerable<UserProjectAllocation> allocations);

    /// <summary>The tracked manager links of one project.</summary>
    Task<IReadOnlyList<ManagerProject>> GetManagerLinksForProjectForUpdateAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>The tracked links of one manager to the given projects.</summary>
    Task<IReadOnlyList<ManagerProject>> GetManagerLinksForUserForUpdateAsync(
        int managerUserId,
        IReadOnlyCollection<int> projectIds,
        CancellationToken cancellationToken = default);

    void AddManagerLink(ManagerProject link);

    void RemoveManagerLinks(IEnumerable<ManagerProject> links);
}
