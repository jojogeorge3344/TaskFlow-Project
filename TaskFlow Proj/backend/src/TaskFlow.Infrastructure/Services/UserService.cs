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
            .Include(u => u.Department)
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

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(u => u.DepartmentId == filter.DepartmentId.Value);
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
                u.DepartmentId,
                u.Department != null ? u.Department.Name : null))
            .ToListAsync(cancellationToken);

        return users;
    }

    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Department)
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
                u.DepartmentId,
                u.Department != null ? u.Department.Name : null))
            .FirstOrDefaultAsync(cancellationToken);

        return user;
    }

    public async Task<UserDto?> GetOwnAsync(int id, CancellationToken cancellationToken)
    {
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLower();
        var username = request.Username.Trim();

        var exists = await _dbContext.Users
            .AnyAsync(u => (!u.IsDeleted) && (u.Email.ToLower() == email || u.Username.ToLower() == username.ToLower()), cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Email or username already exists.");
        }

        if (request.DepartmentId.HasValue)
        {
            var deptExists = await _dbContext.Departments
                .AnyAsync(d => d.Id == request.DepartmentId.Value && !d.IsDeleted, cancellationToken);
            if (!deptExists)
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
            DepartmentId = request.DepartmentId,
            SecurityStamp = NewSecurityStamp(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var departmentName = request.DepartmentId.HasValue
            ? (await _dbContext.Departments.Where(d => d.Id == request.DepartmentId.Value).Select(d => d.Name).FirstAsync(cancellationToken))
            : null;

        return new UserDto(
            user.Id,
            user.EmployeeCode,
            user.FullName,
            user.Username,
            user.Email,
            user.RoleId,
            user.Role.Name,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.DepartmentId,
            departmentName);
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Department)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var email = request.Email.Trim().ToLower();
        var username = request.Username.Trim();

        var exists = await _dbContext.Users
            .AnyAsync(u => u.Id != id && !u.IsDeleted && (u.Email.ToLower() == email || u.Username.ToLower() == username.ToLower()), cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Email or username already exists.");
        }

        if (request.DepartmentId.HasValue)
        {
            var deptExists = await _dbContext.Departments
                .AnyAsync(d => d.Id == request.DepartmentId.Value && !d.IsDeleted, cancellationToken);
            if (!deptExists)
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

        user.EmployeeCode = request.EmployeeCode?.Trim();
        user.FullName = request.FullName.Trim();
        user.Username = username;
        user.Email = email;
        user.RoleId = role.Id;
        user.Role = role;
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

        return new UserDto(
            user.Id,
            user.EmployeeCode,
            user.FullName,
            user.Username,
            user.Email,
            user.RoleId,
            user.Role.Name,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.DepartmentId,
            user.Department?.Name);
    }

    public async Task<UserDto?> UpdateStatusAsync(int id, UserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Department)
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

        return new UserDto(
            user.Id,
            user.EmployeeCode,
            user.FullName,
            user.Username,
            user.Email,
            user.RoleId,
            user.Role.Name,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.DepartmentId,
            user.Department?.Name);
    }

    public async Task<UserDto?> UpdateOwnProfileAsync(int id, UpdateOwnProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.Department)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var email = request.Email!.Trim().ToLower();

        var exists = await _dbContext.Users
            .AnyAsync(u => u.Id != id && !u.IsDeleted && u.Email.ToLower() == email, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Email already exists.");
        }

        user.FullName = request.FullName!.Trim();
        user.Email = email;
        user.UpdatedAt = DateTime.UtcNow;

        // Role, department, username, employee code and status are deliberately untouched.
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UserDto(
            user.Id, user.EmployeeCode, user.FullName, user.Username, user.Email,
            user.RoleId, user.Role.Name, user.IsActive, user.CreatedAt, user.UpdatedAt, user.LastLoginAt,
            user.DepartmentId, user.Department?.Name);
    }

    public async Task<UserDto?> ChangeOwnPasswordAsync(int id, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.Department)
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

        return new UserDto(
            user.Id, user.EmployeeCode, user.FullName, user.Username, user.Email,
            user.RoleId, user.Role.Name, user.IsActive, user.CreatedAt, user.UpdatedAt, user.LastLoginAt,
            user.DepartmentId, user.Department?.Name);
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
