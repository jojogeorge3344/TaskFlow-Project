using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Services;

public class UserService : IUserService
{
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

            if (filter.Role.HasValue)
            {
                query = query.Where(u => u.Role == filter.Role.Value);
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
                u.Role,
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
            .Where(u => u.Id == id && !u.IsDeleted)
            .Select(u => new UserDto(
                u.Id,
                u.EmployeeCode,
                u.FullName,
                u.Username,
                u.Email,
                u.Role,
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

        var user = new User
        {
            EmployeeCode = request.EmployeeCode?.Trim(),
            FullName = request.FullName.Trim(),
            Username = username,
            Email = email,
            Role = request.Role,
            IsActive = request.IsActive,
            DepartmentId = request.DepartmentId,
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
            user.Role,
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

        user.EmployeeCode = request.EmployeeCode?.Trim();
        user.FullName = request.FullName.Trim();
        user.Username = username;
        user.Email = email;
        user.Role = request.Role;
        user.DepartmentId = request.DepartmentId;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UserDto(
            user.Id,
            user.EmployeeCode,
            user.FullName,
            user.Username,
            user.Email,
            user.Role,
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
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UserDto(
            user.Id,
            user.EmployeeCode,
            user.FullName,
            user.Username,
            user.Email,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.DepartmentId,
            user.Department?.Name);
    }
}
