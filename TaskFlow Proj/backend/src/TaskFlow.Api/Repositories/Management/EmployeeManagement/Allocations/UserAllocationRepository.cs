using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Interfaces.Management.EmployeeManagement.Allocations;

namespace TaskFlow.Api.Repositories.Management.EmployeeManagement.Allocations;

public class UserAllocationRepository : BaseRepository<UserProjectAllocation>, IUserAllocationRepository
{
    public UserAllocationRepository(AppDbContext db)
        : base(db)
    {
    }

    public Task<List<UserProjectAllocation>> GetForUserForUpdateAsync(int userId, CancellationToken cancellationToken = default) =>
        Set.Where(a => a.UserId == userId).ToListAsync(cancellationToken);

    public Task<List<UserProjectAllocation>> GetForProjectForUpdateAsync(int projectId, CancellationToken cancellationToken = default) =>
        Set.Where(a => a.ProjectId == projectId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> GetTotalsOnOtherProjectsAsync(
        IReadOnlyCollection<int> userIds,
        int projectId,
        CancellationToken cancellationToken = default) =>
        await Set.Where(a => userIds.Contains(a.UserId) && a.ProjectId != projectId)
            .GroupBy(a => a.UserId)
            .Select(group => new { UserId = group.Key, Total = group.Sum(a => a.AllocationPercent) })
            .ToDictionaryAsync(row => row.UserId, row => row.Total, cancellationToken);

    public void RemoveRange(IEnumerable<UserProjectAllocation> allocations) => Set.RemoveRange(allocations);

    public async Task<IReadOnlyList<ManagerProject>> GetManagerLinksForProjectForUpdateAsync(int projectId, CancellationToken cancellationToken = default) =>
        await Db.ManagerProjects.Where(link => link.ProjectId == projectId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ManagerProject>> GetManagerLinksForUserForUpdateAsync(
        int managerUserId,
        IReadOnlyCollection<int> projectIds,
        CancellationToken cancellationToken = default) =>
        await Db.ManagerProjects
            .Where(link => link.ManagerUserId == managerUserId && projectIds.Contains(link.ProjectId))
            .ToListAsync(cancellationToken);

    public void AddManagerLink(ManagerProject link) => Db.ManagerProjects.Add(link);

    public void RemoveManagerLinks(IEnumerable<ManagerProject> links) => Db.ManagerProjects.RemoveRange(links);
}
