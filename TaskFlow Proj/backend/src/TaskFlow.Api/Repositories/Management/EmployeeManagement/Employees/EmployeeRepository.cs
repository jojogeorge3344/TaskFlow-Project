using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Repositories.Management.EmployeeManagement.Employees;

public class EmployeeRepository : BaseRepository<User>, IEmployeeRepository
{
    public EmployeeRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<UserDto>> GetListAsync(UserListFilter? filter, CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().Where(u => !u.IsDeleted);

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.ToLower();
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(search) ||
                    u.Email.ToLower().Contains(search) ||
                    u.Username.ToLower().Contains(search));
            }

            if (filter.ProjectId.HasValue)
            {
                var projectId = filter.ProjectId.Value;
                query = query.Where(u => u.ProjectAllocations.Any(a => a.ProjectId == projectId));
            }

            if (filter.DepartmentId.HasValue)
            {
                var departmentId = filter.DepartmentId.Value;
                query = query.Where(u => u.DepartmentMemberships.Any(m => m.DepartmentId == departmentId));
            }

            if (!string.IsNullOrWhiteSpace(filter.Role))
            {
                var roleName = filter.Role.Trim();
                query = query.Where(u => u.Role.Name == roleName);
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(u => u.IsActive == filter.IsActive.Value);
            }
        }

        return await LoadDtosAsync(query.OrderBy(u => u.FullName), cancellationToken);
    }

    public async Task<UserDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default) =>
        (await LoadDtosAsync(Set.AsNoTracking().Where(u => u.Id == id && !u.IsDeleted), cancellationToken)).FirstOrDefault();

    public Task<User?> GetActiveWithRoleForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        Set.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);

    public Task<User?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<int> CountHoldingRoleAsync(int roleId, CancellationToken cancellationToken = default) =>
        Set.IgnoreQueryFilters().CountAsync(user => user.RoleId == roleId, cancellationToken);

    public async Task<IReadOnlyList<User>> GetHoldingRoleForUpdateAsync(int roleId, CancellationToken cancellationToken = default) =>
        await Set.IgnoreQueryFilters().Where(user => user.RoleId == roleId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, User>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(user => ids.Contains(user.Id)).ToDictionaryAsync(user => user.Id, cancellationToken);

    public async Task<IReadOnlyList<User>> GetActiveByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive).ToListAsync(cancellationToken);

    public Task<bool> EmailPrefixTakenAsync(string emailPrefix, int? exceptUserId, CancellationToken cancellationToken = default) =>
        Set.IgnoreQueryFilters()
            .AnyAsync(u => (exceptUserId == null || u.Id != exceptUserId) && u.Email.ToLower().StartsWith(emailPrefix), cancellationToken);

    public Task<bool> UsernameTakenAsync(string username, int? exceptUserId, CancellationToken cancellationToken = default) =>
        Set.IgnoreQueryFilters()
            .AnyAsync(u => (exceptUserId == null || u.Id != exceptUserId) && u.Username.ToLower() == username.ToLower(), cancellationToken);

    public Task<bool> EmailAndMobileTakenAsync(string email, string mobileNumber, int exceptUserId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(u => u.Id != exceptUserId && u.Email == email && u.MobileNumber == mobileNumber, cancellationToken);

    public Task<int> CountOtherActiveAdministratorsAsync(int userId, CancellationToken cancellationToken = default) =>
        Set.CountAsync(u => u.Id != userId && !u.IsDeleted && u.IsActive && u.RoleId == SystemRoles.AdministratorId, cancellationToken);

    public async System.Threading.Tasks.Task ClearManagerDutiesAsync(int managerUserId, CancellationToken cancellationToken = default)
    {
        Db.ManagerProjects.RemoveRange(
            await Db.ManagerProjects.Where(link => link.ManagerUserId == managerUserId).ToListAsync(cancellationToken));
        foreach (var allocation in await Db.UserProjectAllocations
                     .Where(a => a.UserId == managerUserId && a.IsProjectManager)
                     .ToListAsync(cancellationToken))
        {
            allocation.IsProjectManager = false;
        }

        Db.DepartmentManagers.RemoveRange(
            await Db.DepartmentManagers.Where(link => link.ManagerUserId == managerUserId).ToListAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<UserDepartment>> GetDepartmentMembershipsForUpdateAsync(int userId, CancellationToken cancellationToken = default) =>
        await Db.UserDepartments.Where(link => link.UserId == userId).ToListAsync(cancellationToken);

    public void RemoveDepartmentMemberships(IEnumerable<UserDepartment> memberships) => Db.UserDepartments.RemoveRange(memberships);

    public Task<User?> GetLiveAsync(int id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(user => user.Id == id && !user.IsDeleted, cancellationToken);

    public Task<int> CountLiveAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        Set.CountAsync(user => ids.Contains(user.Id) && !user.IsDeleted, cancellationToken);

    public Task<string?> GetFullNameAsync(int id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().Where(user => user.Id == id).Select(user => user.FullName).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetActiveEmailsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(user => ids.Contains(user.Id) && user.IsActive && !user.IsDeleted)
            .Select(user => user.Email)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> GetTeamMemberIdsAsync(int managerId, CancellationToken cancellationToken = default)
    {
        var managedDepartments = Db.DepartmentManagers
            .Where(link => link.ManagerUserId == managerId)
            .Select(link => link.DepartmentId);

        // The projects this person is flagged as a manager of: their members belong to the team as well.
        var managedProjects = Db.UserProjectAllocations
            .Where(allocation => allocation.UserId == managerId && allocation.IsProjectManager)
            .Select(allocation => allocation.ProjectId);

        return await Set
            .Where(user => user.IsActive && user.Id != managerId
                && (user.DepartmentMemberships.Any(m => managedDepartments.Contains(m.DepartmentId))
                    || user.ProjectAllocations.Any(a => managedProjects.Contains(a.ProjectId))))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetLiveIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(user => ids.Contains(user.Id)).Select(user => user.Id).ToListAsync(cancellationToken);

    public Task<string?> GetFullNameIncludingDeletedAsync(int id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().IgnoreQueryFilters().Where(user => user.Id == id).Select(user => user.FullName).FirstOrDefaultAsync(cancellationToken);

    /// <summary>Loads users as DTOs. A user's managers are the managers of their departments plus the flagged managers of the
    /// projects they are on, and a manager's reports are the people in the departments they manage plus the people on the
    /// projects they manage; neither is stored on the user, so both are read in their own batched queries (a correlated
    /// subquery per row cannot be translated on SQLite).</summary>
    private async Task<List<UserDto>> LoadDtosAsync(IQueryable<User> users, CancellationToken cancellationToken)
    {
        var empty = (IReadOnlyList<UserManagerDto>)Array.Empty<UserManagerDto>();
        var emptyReports = (IReadOnlyList<ManagerReportDto>)Array.Empty<ManagerReportDto>();

        var dtos = await users.Select(u => new UserDto(
            u.Id,
            u.EmployeeCode,
            u.FullName,
            u.Username,
            u.Email,
            u.RoleId,
            u.Role.Name,
            u.IsActive,
            u.CreatedAt,
            u.UpdatedAt,
            u.LastLoginAt,
            u.ProjectAllocations
                .OrderBy(a => a.Project.Name)
                .Select(a => new ProjectAllocationDto(a.ProjectId, a.Project.Name, a.PositionId, a.Position.Name, a.AllocationPercent))
                .ToList(),
            u.DepartmentMemberships
                .OrderBy(m => m.Department.Name)
                .Select(m => new UserDepartmentDto(m.DepartmentId, m.Department.Name))
                .ToList(),
            empty,
            u.ManagerProjects
                .OrderBy(link => link.Project.Name)
                .Select(link => new ManagerProjectDto(link.ProjectId, link.Project.Name))
                .ToList(),
            emptyReports,
            u.MfaEnabledAt != null,
            u.MfaBackupCodes.Count(code => code.UsedAt == null),
            u.MobileNumber)).ToListAsync(cancellationToken);

        if (dtos.Count == 0)
        {
            return dtos;
        }

        var ids = dtos.Select(dto => dto.Id).ToList();

        var managerRows = await (
            from membership in Db.UserDepartments
            join link in Db.DepartmentManagers on membership.DepartmentId equals link.DepartmentId
            join manager in Db.Users on link.ManagerUserId equals manager.Id
            where ids.Contains(membership.UserId) && manager.Id != membership.UserId
            select new { membership.UserId, ManagerId = manager.Id, manager.FullName })
            .Distinct()
            .ToListAsync(cancellationToken);
        var projectManagerRows = await (
            from member in Db.UserProjectAllocations
            join lead in Db.UserProjectAllocations on member.ProjectId equals lead.ProjectId
            join manager in Db.Users on lead.UserId equals manager.Id
            where lead.IsProjectManager && ids.Contains(member.UserId) && lead.UserId != member.UserId
            select new { member.UserId, ManagerId = manager.Id, manager.FullName })
            .Distinct()
            .ToListAsync(cancellationToken);
        var managersByUser = managerRows
            .Concat(projectManagerRows)
            .DistinctBy(row => (row.UserId, row.ManagerId))
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<UserManagerDto>)group
                    .OrderBy(row => row.FullName).ThenBy(row => row.ManagerId)
                    .Select(row => new UserManagerDto(row.ManagerId, row.FullName))
                    .ToList());

        var reportRows = await (
            from link in Db.DepartmentManagers
            join membership in Db.UserDepartments on link.DepartmentId equals membership.DepartmentId
            join report in Db.Users on membership.UserId equals report.Id
            where ids.Contains(link.ManagerUserId) && report.Id != link.ManagerUserId
            select new { link.ManagerUserId, ReportId = report.Id, report.FullName })
            .Distinct()
            .ToListAsync(cancellationToken);
        var projectReportRows = await (
            from lead in Db.UserProjectAllocations
            join member in Db.UserProjectAllocations on lead.ProjectId equals member.ProjectId
            join report in Db.Users on member.UserId equals report.Id
            where lead.IsProjectManager && ids.Contains(lead.UserId) && report.Id != lead.UserId
            select new { ManagerUserId = lead.UserId, ReportId = report.Id, report.FullName })
            .Distinct()
            .ToListAsync(cancellationToken);
        var reportsByManager = reportRows
            .Concat(projectReportRows)
            .DistinctBy(row => (row.ManagerUserId, row.ReportId))
            .GroupBy(row => row.ManagerUserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ManagerReportDto>)group
                    .OrderBy(row => row.FullName).ThenBy(row => row.ReportId)
                    .Select(row => new ManagerReportDto(row.ReportId, row.FullName))
                    .ToList());

        return dtos
            .Select(dto => dto with
            {
                Managers = managersByUser.GetValueOrDefault(dto.Id) ?? empty,
                Reports = reportsByManager.GetValueOrDefault(dto.Id) ?? emptyReports
            })
            .ToList();
    }
}
