using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Services;

public class UserService : IUserService
{
    private const int MinPasswordLength = 6;

    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IUserAllocationService _allocations;

    public UserService(AppDbContext dbContext, IPasswordHasher<User> passwordHasher, IUserAllocationService allocations)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _allocations = allocations;
    }

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
            from membership in _dbContext.UserDepartments
            join link in _dbContext.DepartmentManagers on membership.DepartmentId equals link.DepartmentId
            join manager in _dbContext.Users on link.ManagerUserId equals manager.Id
            where ids.Contains(membership.UserId) && manager.Id != membership.UserId
            select new { membership.UserId, ManagerId = manager.Id, manager.FullName })
            .Distinct()
            .ToListAsync(cancellationToken);
        var projectManagerRows = await (
            from member in _dbContext.UserProjectAllocations
            join lead in _dbContext.UserProjectAllocations on member.ProjectId equals lead.ProjectId
            join manager in _dbContext.Users on lead.UserId equals manager.Id
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
            from link in _dbContext.DepartmentManagers
            join membership in _dbContext.UserDepartments on link.DepartmentId equals membership.DepartmentId
            join report in _dbContext.Users on membership.UserId equals report.Id
            where ids.Contains(link.ManagerUserId) && report.Id != link.ManagerUserId
            select new { link.ManagerUserId, ReportId = report.Id, report.FullName })
            .Distinct()
            .ToListAsync(cancellationToken);
        var projectReportRows = await (
            from lead in _dbContext.UserProjectAllocations
            join member in _dbContext.UserProjectAllocations on lead.ProjectId equals member.ProjectId
            join report in _dbContext.Users on member.UserId equals report.Id
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

    public async Task<IReadOnlyCollection<UserDto>> GetAllAsync(UserListFilter? filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .AsQueryable();

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

    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var users = await LoadDtosAsync(_dbContext.Users.AsNoTracking().Where(u => u.Id == id && !u.IsDeleted), cancellationToken);
        return users.FirstOrDefault();
    }

    public async Task<UserDto?> GetOwnAsync(int id, CancellationToken cancellationToken)
    {
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = RequireValidEmail(request.Email);
        var username = request.Username.Trim();

        if (await EmailTakenAsync(email, null, cancellationToken))
        {
            throw new InvalidOperationException("Email already exists.");
        }

        // Deleted users keep their username (the unique index still covers them), so they count here too.
        var usernameTaken = await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Username.ToLower() == username.ToLower(), cancellationToken);

        if (usernameTaken)
        {
            throw new InvalidOperationException("Email or username already exists.");
        }

        if (request.ProjectAllocations is not null)
        {
            await _allocations.ValidateForUserAsync(request.ProjectAllocations, cancellationToken);
        }

        if (request.DepartmentIds is not null)
        {
            await _allocations.ValidateDepartmentsAsync(request.DepartmentIds, cancellationToken);
        }

        var role = await RequireRoleAsync(request.RoleId, cancellationToken);

        var user = new User
        {
            EmployeeCode = request.EmployeeCode?.Trim(),
            FullName = request.FullName.Trim(),
            Username = username,
            Email = email,
            RoleId = role.Id,
            Role = role,
            IsActive = request.IsActive,
            SecurityStamp = NewSecurityStamp(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        await _allocations.StageForUserAsync(user, request.ProjectAllocations, request.DepartmentIds, cancellationToken);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var email = RequireValidEmail(request.Email);
        var username = request.Username.Trim();

        if (await EmailTakenAsync(email, id, cancellationToken))
        {
            throw new InvalidOperationException("Email already exists.");
        }

        var usernameTaken = await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Id != id && u.Username.ToLower() == username.ToLower(), cancellationToken);

        if (usernameTaken)
        {
            throw new InvalidOperationException("Email or username already exists.");
        }

        if (request.ProjectAllocations is not null)
        {
            await _allocations.ValidateForUserAsync(request.ProjectAllocations, cancellationToken);
        }

        if (request.DepartmentIds is not null)
        {
            await _allocations.ValidateDepartmentsAsync(request.DepartmentIds, cancellationToken);
        }

        // An empty password keeps the current one; anything else must be a real password.
        var passwordChanged = !string.IsNullOrEmpty(request.Password);
        if (passwordChanged && string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Password cannot be blank.");
        }
        if (passwordChanged && request.Password!.Length < MinPasswordLength)
        {
            throw new InvalidOperationException($"Password must be at least {MinPasswordLength} characters.");
        }

        var role = await RequireRoleAsync(request.RoleId, cancellationToken);

        var usernameChanged = !string.Equals(user.Username, username, StringComparison.Ordinal);
        var roleChanged = user.RoleId != request.RoleId;

        if (roleChanged)
        {
            await EnsureAnotherActiveAdministratorAsync(
                user, "The only active Administrator cannot be moved to another role.", cancellationToken);
        }

        var now = DateTime.UtcNow;

        if (roleChanged && user.RoleId == SystemRoles.ManagerId)
        {
            // Someone who is no longer a Manager oversees no projects and has no reports.
            await ClearManagerDutiesAsync(user.Id, now, cancellationToken);
        }

        user.EmployeeCode = request.EmployeeCode?.Trim();
        user.FullName = request.FullName.Trim();
        user.Username = username;
        user.Email = email;
        user.RoleId = role.Id;
        user.Role = role;
        await _allocations.StageForUserAsync(user, request.ProjectAllocations, request.DepartmentIds, cancellationToken);
        user.UpdatedAt = now;

        if (passwordChanged)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password!);
        }

        // A role change ends sessions too, so a demoted user cannot keep the old role's access token.
        if (usernameChanged || passwordChanged || roleChanged)
        {
            await EndSessionsAsync(user, now, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserDto?> UpdateStatusAsync(int id, UserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (user.IsActive && !request.IsActive)
        {
            await EnsureAnotherActiveAdministratorAsync(
                user, "The only active Administrator cannot be deactivated.", cancellationToken);

            // Deactivation ends sessions for good, so reactivating later cannot revive an old refresh cookie.
            await EndSessionsAsync(user, now, cancellationToken);
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserDto?> UpdateOwnProfileAsync(int id, UpdateOwnProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var email = RequireValidEmail(request.Email);

        // Null keeps the current number; an empty value clears it.
        var mobileNumber = request.MobileNumber is null
            ? user.MobileNumber
            : MobileNumberPolicy.Normalize(request.MobileNumber);
        if (mobileNumber is not null && !MobileNumberPolicy.IsValid(mobileNumber))
        {
            throw new InvalidOperationException(MobileNumberPolicy.InvalidMessage);
        }

        // Email is the only unique identity, so a number may be shared. What is refused is a second record that already holds
        // this exact email and number together.
        if (mobileNumber is not null
            && await _dbContext.Users.AnyAsync(u => u.Id != id && u.Email == email && u.MobileNumber == mobileNumber, cancellationToken))
        {
            throw new InvalidOperationException("A user with this email and mobile number already exists.");
        }

        if (await EmailTakenAsync(email, id, cancellationToken))
        {
            throw new InvalidOperationException("Email already exists.");
        }

        user.FullName = request.FullName!.Trim();
        user.Email = email;
        user.MobileNumber = mobileNumber;
        user.UpdatedAt = DateTime.UtcNow;

        // Role, project, username, employee code and status are deliberately untouched.
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserDto?> ChangeOwnPasswordAsync(int id, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword!);
        user.UpdatedAt = now;

        // The password change ends every session, including this one; the client signs in again.
        await EndSessionsAsync(user, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    /// <summary>Removes a manager's project and department oversight. Their reports follow, because a user's managers are
    /// derived from the departments they manage and the projects they are flagged on.</summary>
    private async System.Threading.Tasks.Task ClearManagerDutiesAsync(int managerUserId, DateTime now, CancellationToken cancellationToken)
    {
        _dbContext.ManagerProjects.RemoveRange(
            await _dbContext.ManagerProjects.Where(link => link.ManagerUserId == managerUserId).ToListAsync(cancellationToken));
        foreach (var allocation in await _dbContext.UserProjectAllocations
                     .Where(a => a.UserId == managerUserId && a.IsProjectManager)
                     .ToListAsync(cancellationToken))
        {
            allocation.IsProjectManager = false;
        }
        _dbContext.DepartmentManagers.RemoveRange(
            await _dbContext.DepartmentManagers.Where(link => link.ManagerUserId == managerUserId).ToListAsync(cancellationToken));
    }

    public async Task<bool> DeleteAsync(int id, int currentUserId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return false;
        }

        if (user.Id == currentUserId)
        {
            throw new InvalidOperationException("You cannot delete your own account.");
        }

        await EnsureAnotherActiveAdministratorAsync(
            user, "The only active Administrator cannot be deleted.", cancellationToken);

        var unfinished = await _dbContext.Tasks
            .CountAsync(
                t => t.AssignedToUserId == id
                     && !t.IsDeleted
                     && t.Status != Domain.Enums.TaskStatus.Completed
                     && t.Status != Domain.Enums.TaskStatus.Cancelled,
                cancellationToken);
        if (unfinished > 0)
        {
            throw new InvalidOperationException(
                $"{user.FullName} still has {unfinished} unfinished task{(unfinished == 1 ? string.Empty : "s")}. Reassign {(unfinished == 1 ? "it" : "them")} before deleting this employee.");
        }

        var now = DateTime.UtcNow;

        // Their reports have no manager any more, and a deleted manager oversees no projects.
        await ClearManagerDutiesAsync(id, now, cancellationToken);

        // The email and username stay on the row, so they remain reserved and history is never ambiguous.
        await EndSessionsAsync(user, now, cancellationToken);
        // Allocations and memberships stay on the row, so reports keep a deleted person's projects; the soft-delete filters
        // already keep them out of every total and list.
        user.IsActive = false;
        user.IsDeleted = true;
        user.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string RequireValidEmail(string? email)
    {
        var normalized = EmailPolicy.Normalize(email);
        if (!EmailPolicy.IsValid(normalized))
        {
            throw new InvalidOperationException(EmailPolicy.InvalidMessage);
        }

        return normalized;
    }

    /// <summary>The domain is fixed, so two addresses are the same person's mailbox when their local parts match.
    /// Deleted users still count: their address stays reserved so history is never ambiguous.</summary>
    private async Task<bool> EmailTakenAsync(string normalizedEmail, int? exceptUserId, CancellationToken cancellationToken)
    {
        var prefix = EmailPolicy.LocalPart(normalizedEmail) + "@";
        return await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => (exceptUserId == null || u.Id != exceptUserId) && u.Email.ToLower().StartsWith(prefix), cancellationToken);
    }

    /// <summary>Keeps at least one active Administrator, the only role that can manage roles.</summary>
    private async System.Threading.Tasks.Task EnsureAnotherActiveAdministratorAsync(User user, string message, CancellationToken cancellationToken)
    {
        if (user.RoleId != SystemRoles.AdministratorId || !user.IsActive)
        {
            return;
        }

        var others = await _dbContext.Users
            .CountAsync(u => u.Id != user.Id && !u.IsDeleted && u.IsActive && u.RoleId == SystemRoles.AdministratorId, cancellationToken);
        if (others == 0)
        {
            throw new InvalidOperationException(message);
        }
    }

    private async Task<Role> RequireRoleAsync(int roleId, CancellationToken cancellationToken) =>
        await _dbContext.Roles.FirstOrDefaultAsync(role => role.Id == roleId, cancellationToken)
        ?? throw new InvalidOperationException("Role does not exist.");

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");
    /// <summary>
    /// Ends every existing session of the user: access tokens carrying the old stamp fail validation,
    /// and their refresh tokens are revoked so no new ones can be minted. Saved by the caller.
    /// </summary>
    private async System.Threading.Tasks.Task EndSessionsAsync(User user, DateTime now, CancellationToken cancellationToken)
    {
        user.SecurityStamp = NewSecurityStamp();

        var activeTokens = await _dbContext.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }
    }
}
