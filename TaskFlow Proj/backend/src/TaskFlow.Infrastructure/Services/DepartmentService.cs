using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Departments;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Services;

public class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _dbContext;

    public DepartmentService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DepartmentWithCountDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var departments = await _dbContext.Departments
            .AsNoTracking()
            .Select(d => new DepartmentWithCountDto(
                d.Id,
                d.Name,
                d.Description,
                d.CreatedAt,
                _dbContext.Users.Count(u => u.DepartmentId == d.Id && !u.IsDeleted)))
            .ToListAsync(cancellationToken);

        return departments;
    }

    public async Task<DepartmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var department = await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Description, d.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return department;
    }

    public async Task<IReadOnlyCollection<User>> GetEmployeesAsync(int departmentId, CancellationToken cancellationToken)
    {
        var employees = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.DepartmentId == departmentId && !u.IsDeleted)
            .ToListAsync(cancellationToken);

        return employees;
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var exists = await _dbContext.Departments
            .AnyAsync(d => d.Name.ToLower() == name.ToLower() && !d.IsDeleted, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Project name already exists.");
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
            throw new InvalidOperationException("Project name already exists.");
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

        var hasEmployees = await _dbContext.Users
            .AnyAsync(u => u.DepartmentId == id && !u.IsDeleted, cancellationToken);

        if (hasEmployees)
        {
            throw new InvalidOperationException("Project cannot be deleted because it has associated employees.");
        }

        // The department is only flagged, so the manager links to it would not cascade away on their own.
        _dbContext.ManagerDepartments.RemoveRange(
            await _dbContext.ManagerDepartments.Where(link => link.DepartmentId == id).ToListAsync(cancellationToken));

        department.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
