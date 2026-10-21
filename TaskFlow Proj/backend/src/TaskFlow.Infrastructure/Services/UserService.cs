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

    public UserService(AppDbContext dbContext, IPasswordHasher<User> passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyCollection<UserDto>> GetAllAsync(UserListFilter? filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Project)
            .Include(u => u.Role)
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
                query = query.Where(u => u.ProjectId == filter.ProjectId.Value);
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

        var users = await query
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto(
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
                u.ProjectId,
                u.Project != null ? u.Project.Name : null,
                u.DepartmentId,
                u.Department != null ? u.Department.Name : null,
                u.ManagerId,
                u.Manager != null ? u.Manager.FullName : null,
                u.ManagerProjects
                    .OrderBy(link => link.Project.Name)
                    .Select(link => new ManagerProjectDto(link.ProjectId, link.Project.Name))
                    .ToList(),
                u.Reports
                    .OrderBy(report => report.FullName)
                    .Select(report => new ManagerReportDto(report.Id, report.FullName))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return users;
    }

    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Project)
            .Include(u => u.Role)
            .Where(u => u.Id == id && !u.IsDeleted)
            .Select(u => new UserDto(
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
                u.ProjectId,
                u.Project != null ? u.Project.Name : null,
                u.DepartmentId,
                u.Department != null ? u.Department.Name : null,
                u.ManagerId,
                u.Manager != null ? u.Manager.FullName : null,
                u.ManagerProjects
                    .OrderBy(link => link.Project.Name)
                    .Select(link => new ManagerProjectDto(link.ProjectId, link.Project.Name))
                    .ToList(),
                u.Reports
                    .OrderBy(report => report.FullName)
                    .Select(report => new ManagerReportDto(report.Id, report.FullName))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return user;
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

        if (request.ProjectId.HasValue)
        {
            var projectExists = await _dbContext.Projects
                .AnyAsync(p => p.Id == request.ProjectId.Value && !p.IsDeleted, cancellationToken);
            if (!projectExists)
            {
                throw new InvalidOperationException("Project does not exist.");
            }
        }

        if (request.DepartmentId.HasValue)
        {
            var departmentExists = await _dbContext.Departments
                .AnyAsync(d => d.Id == request.DepartmentId.Value && !d.IsDeleted, cancellationToken);
            if (!departmentExists)
            {
                throw new InvalidOperationException("Department does not exist.");
            }
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
            ProjectId = request.ProjectId,
            DepartmentId = request.DepartmentId,
            SecurityStamp = NewSecurityStamp(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Project)
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

        if (request.ProjectId.HasValue)
        {
            var projectExists = await _dbContext.Projects
                .AnyAsync(p => p.Id == request.ProjectId.Value && !p.IsDeleted, cancellationToken);
            if (!projectExists)
            {
                throw new InvalidOperationException("Project does not exist.");
            }
        }

        if (request.DepartmentId.HasValue)
        {
            var departmentExists = await _dbContext.Departments
                .AnyAsync(d => d.Id == request.DepartmentId.Value && !d.IsDeleted, cancellationToken);
            if (!departmentExists)
            {
                throw new InvalidOperationException("Department does not exist.");
            }
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
        user.ProjectId = request.ProjectId;
        user.DepartmentId = request.DepartmentId;
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
            .Include(u => u.Project)
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
            .Include(u => u.Project)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var email = RequireValidEmail(request.Email);

        if (await EmailTakenAsync(email, id, cancellationToken))
        {
            throw new InvalidOperationException("Email already exists.");
        }

        user.FullName = request.FullName!.Trim();
        user.Email = email;
        user.UpdatedAt = DateTime.UtcNow;

        // Role, project, username, employee code and status are deliberately untouched.
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserDto?> ChangeOwnPasswordAsync(int id, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.Project)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword!);
        if (verification != PasswordVerificationResult.Success)
        {
            throw new InvalidOperationException("Current password is incorrect.");
        }

        var now = DateTime.UtcNow;
        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword!);
        user.UpdatedAt = now;

        // The password change ends every session, including this one; the client signs in again.
        await EndSessionsAsync(user, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserDto?> UpdateManagerAssignmentsAsync(int id, ManagerAssignmentRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var projectIds = request.ManagerProjectIds?.Distinct().ToList() ?? [];
        if (projectIds.Count > 0)
        {
            if (user.RoleId != SystemRoles.ManagerId)
            {
                throw new InvalidOperationException("Only a Manager can be assigned projects.");
            }

            var existing = await _dbContext.Projects.CountAsync(p => projectIds.Contains(p.Id), cancellationToken);
            if (existing != projectIds.Count)
            {
                throw new InvalidOperationException("One or more projects do not exist.");
            }
        }

        if (request.ManagerId is int managerId)
        {
            if (managerId == id)
            {
                throw new InvalidOperationException("A user cannot be their own manager.");
            }

            var manager = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == managerId, cancellationToken);
            if (manager is null || manager.RoleId != SystemRoles.ManagerId || !manager.IsActive)
            {
                throw new InvalidOperationException("The manager must be an active Manager.");
            }

            // Walking up from the chosen manager must never come back to this user.
            var cursor = manager.ManagerId;
            for (var steps = 0; cursor is int above && steps < 100; steps++)
            {
                if (above == id)
                {
                    throw new InvalidOperationException("That would make two people each other's manager.");
                }

                cursor = await _dbContext.Users.Where(u => u.Id == above).Select(u => u.ManagerId).FirstOrDefaultAsync(cancellationToken);
            }
        }

        var links = await _dbContext.ManagerProjects.Where(link => link.ManagerUserId == id).ToListAsync(cancellationToken);
        _dbContext.ManagerProjects.RemoveRange(links.Where(link => !projectIds.Contains(link.ProjectId)));
        foreach (var projectId in projectIds.Where(projectId => links.All(link => link.ProjectId != projectId)))
        {
            _dbContext.ManagerProjects.Add(new ManagerProject { ManagerUserId = id, ProjectId = projectId });
        }

        user.ManagerId = request.ManagerId;
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>Clears the manager link of everyone who reports to this user and removes their project oversight.</summary>
    private async System.Threading.Tasks.Task ClearManagerDutiesAsync(int managerUserId, DateTime now, CancellationToken cancellationToken)
    {
        var reports = await _dbContext.Users.Where(u => u.ManagerId == managerUserId).ToListAsync(cancellationToken);
        foreach (var report in reports)
        {
            report.ManagerId = null;
            report.UpdatedAt = now;
        }

        _dbContext.ManagerProjects.RemoveRange(
            await _dbContext.ManagerProjects.Where(link => link.ManagerUserId == managerUserId).ToListAsync(cancellationToken));
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
        user.ManagerId = null;
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
