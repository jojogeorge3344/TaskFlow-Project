using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Departments;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Infrastructure.Services;

public class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _dbContext;

    public DepartmentService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DepartmentWithCountsDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var departments = await _dbContext.Departments
            .AsNoTracking()
            .Select(d => new DepartmentWithCountsDto(
                d.Id,
                d.Name,
                d.Description,
                d.CreatedAt,
                _dbContext.DepartmentManagers.Count(link => link.DepartmentId == d.Id
                    && _dbContext.Users.Any(u => u.Id == link.ManagerUserId)),
                _dbContext.UserDepartments.Count(m => m.DepartmentId == d.Id)))
            .ToListAsync(cancellationToken);
        return departments;
    }

    public async Task<DepartmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Description, d.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var exists = await _dbContext.Departments
            .AnyAsync(d => d.Name.ToLower() == name.ToLower() && !d.IsDeleted, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Department name already exists.");
        }

        var department = new Department
        {
            Name = name,
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Departments.Add(department);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DepartmentDto(department.Id, department.Name, department.Description, department.CreatedAt);
    }

    public async Task<DepartmentDto?> UpdateAsync(int id, UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);
        if (department is null)
        {
            return null;
        }

        var name = request.Name.Trim();
        var exists = await _dbContext.Departments
            .AnyAsync(d => d.Id != id && d.Name.ToLower() == name.ToLower() && !d.IsDeleted, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Department name already exists.");
        }

        department.Name = name;
        department.Description = request.Description?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DepartmentDto(department.Id, department.Name, department.Description, department.CreatedAt);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var department = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);
        if (department is null)
        {
            return false;
        }

        // Users has a soft-delete filter, so a link left behind by a deleted manager does not count as a member.
        var hasManagers = await _dbContext.DepartmentManagers
            .AnyAsync(link => link.DepartmentId == id && _dbContext.Users.Any(u => u.Id == link.ManagerUserId), cancellationToken);
        var hasEmployees = await _dbContext.UserDepartments
            .AnyAsync(membership => membership.DepartmentId == id, cancellationToken);
        if (hasManagers || hasEmployees)
        {
            throw new InvalidOperationException("Department cannot be deleted because it still has members. Remove them first.");
        }

        // The department is only flagged, so the manager links to it would not cascade away on their own.
        _dbContext.DepartmentManagers.RemoveRange(
            await _dbContext.DepartmentManagers.Where(link => link.DepartmentId == id).ToListAsync(cancellationToken));

        department.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<DepartmentMembersDto?> GetMembersAsync(int departmentId, CancellationToken cancellationToken)
    {
        var departmentExists = await _dbContext.Departments
            .AnyAsync(d => d.Id == departmentId && !d.IsDeleted, cancellationToken);
        if (!departmentExists)
        {
            return null;
        }

        var managers = await _dbContext.Users
            .AsNoTracking()
            .Where(u => _dbContext.DepartmentManagers.Any(link => link.DepartmentId == departmentId && link.ManagerUserId == u.Id))
            .OrderBy(u => u.FullName)
            .Select(u => new MemberRefDto(u.Id, u.FullName, u.Username, u.Email, u.IsActive))
            .ToListAsync(cancellationToken);

        var employees = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.DepartmentMemberships.Any(m => m.DepartmentId == departmentId))
            .OrderBy(u => u.FullName)
            .Select(u => new MemberRefDto(u.Id, u.FullName, u.Username, u.Email, u.IsActive))
            .ToListAsync(cancellationToken);

        return new DepartmentMembersDto(departmentId, managers, employees);
    }

    public async Task<DepartmentMembersDto?> ReplaceMembersAsync(int departmentId, UpdateDepartmentMembersRequest request, CancellationToken cancellationToken)
    {
        var department = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.Id == departmentId && !d.IsDeleted, cancellationToken);
        if (department is null)
        {
            return null;
        }

        var managerIds = request.ManagerIds.Distinct().ToList();
        var employeeIds = request.EmployeeIds.Distinct().ToList();

        // Every manager must be an active Manager; every employee an active, non-deleted user.
        // One bad id rejects the whole request and leaves membership unchanged.
        var managerById = await _dbContext.Users
            .Where(u => managerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
        foreach (var managerId in managerIds)
        {
            if (!managerById.TryGetValue(managerId, out var user)
                || !user.IsActive
                || user.RoleId != SystemRoles.ManagerId)
            {
                throw new InvalidOperationException($"User {managerId} is not an active manager.");
            }
        }

        var employeeById = await _dbContext.Users
            .Where(u => employeeIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
        foreach (var employeeId in employeeIds)
        {
            if (!employeeById.TryGetValue(employeeId, out var user) || !user.IsActive)
            {
                throw new InvalidOperationException($"User {employeeId} is not an active user.");
            }
        }

        var existingLinks = await _dbContext.DepartmentManagers
            .Where(link => link.DepartmentId == departmentId)
            .ToListAsync(cancellationToken);
        _dbContext.DepartmentManagers.RemoveRange(
            existingLinks.Where(link => !managerIds.Contains(link.ManagerUserId)));
        foreach (var managerId in managerIds.Where(id => existingLinks.All(link => link.ManagerUserId != id)))
        {
            _dbContext.DepartmentManagers.Add(new DepartmentManager { DepartmentId = departmentId, ManagerUserId = managerId });
        }

        // A person may belong to several departments, so only this department's membership rows change.
        var existingMemberships = await _dbContext.UserDepartments
            .Where(membership => membership.DepartmentId == departmentId)
            .ToListAsync(cancellationToken);
        _dbContext.UserDepartments.RemoveRange(existingMemberships.Where(membership => !employeeIds.Contains(membership.UserId)));
        foreach (var employeeId in employeeIds.Where(id => existingMemberships.All(membership => membership.UserId != id)))
        {
            _dbContext.UserDepartments.Add(new UserDepartment { DepartmentId = departmentId, UserId = employeeId });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetMembersAsync(departmentId, cancellationToken);
    }
}
