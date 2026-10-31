using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Dtos.Management.DepartmentManagement;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Management.DepartmentManagement;

public class DepartmentRepository : BaseRepository<Department>, IDepartmentRepository
{
    public DepartmentRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<DepartmentWithCountsDto>> GetAllWithCountsAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Select(d => new DepartmentWithCountsDto(
                d.Id,
                d.Name,
                d.Description,
                d.CreatedAt,
                Db.DepartmentManagers.Count(link => link.DepartmentId == d.Id && Db.Users.Any(u => u.Id == link.ManagerUserId)),
                Db.UserDepartments.Count(m => m.DepartmentId == d.Id)))
            .ToListAsync(cancellationToken);

    public Task<Department?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Department?> GetActiveForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);

    public Task<bool> ExistsActiveAsync(int id, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);

    public Task<int> CountActiveAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        Set.CountAsync(d => ids.Contains(d.Id) && !d.IsDeleted, cancellationToken);

    public Task<bool> NameTakenAsync(string name, int? exceptDepartmentId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(d => d.Id != exceptDepartmentId && d.Name.ToLower() == name.ToLower() && !d.IsDeleted, cancellationToken);

    public async Task<bool> HasMembersAsync(int id, CancellationToken cancellationToken = default) =>
        await Db.DepartmentManagers.AnyAsync(link => link.DepartmentId == id && Db.Users.Any(u => u.Id == link.ManagerUserId), cancellationToken)
        || await Db.UserDepartments.AnyAsync(membership => membership.DepartmentId == id, cancellationToken);

    public async Task<IReadOnlyList<MemberRefDto>> GetManagersAsync(int id, CancellationToken cancellationToken = default) =>
        await Db.Users.AsNoTracking()
            .Where(u => Db.DepartmentManagers.Any(link => link.DepartmentId == id && link.ManagerUserId == u.Id))
            .OrderBy(u => u.FullName)
            .Select(u => new MemberRefDto(u.Id, u.FullName, u.Username, u.Email, u.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MemberRefDto>> GetEmployeesAsync(int id, CancellationToken cancellationToken = default) =>
        await Db.Users.AsNoTracking()
            .Where(u => u.DepartmentMemberships.Any(m => m.DepartmentId == id))
            .OrderBy(u => u.FullName)
            .Select(u => new MemberRefDto(u.Id, u.FullName, u.Username, u.Email, u.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DepartmentManager>> GetManagerLinksForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        await Db.DepartmentManagers.Where(link => link.DepartmentId == id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserDepartment>> GetMembershipsForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        await Db.UserDepartments.Where(membership => membership.DepartmentId == id).ToListAsync(cancellationToken);

    public void AddManagerLink(DepartmentManager link) => Db.DepartmentManagers.Add(link);

    public void RemoveManagerLinks(IEnumerable<DepartmentManager> links) => Db.DepartmentManagers.RemoveRange(links);

    public void AddMembership(UserDepartment membership) => Db.UserDepartments.Add(membership);

    public void RemoveMemberships(IEnumerable<UserDepartment> memberships) => Db.UserDepartments.RemoveRange(memberships);
}
