using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Management.DepartmentManagement;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Repositories.Management.DepartmentManagement;
using TaskFlow.Api.Repositories.Management.EmployeeManagement;

namespace TaskFlow.Api.Services.Management.DepartmentManagement;

public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _departments;
    private readonly IEmployeeRepository _employees;
    private readonly IUnitOfWork _unitOfWork;

    public DepartmentService(IDepartmentRepository departments, IEmployeeRepository employees, IUnitOfWork unitOfWork)
    {
        _departments = departments;
        _employees = employees;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyCollection<DepartmentWithCountsDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await _departments.GetAllWithCountsAsync(cancellationToken);

    public async Task<DepartmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await _departments.GetAsync(id, cancellationToken) is { } department ? ToDto(department) : null;

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _departments.NameTakenAsync(name, exceptDepartmentId: null, cancellationToken))
        {
            throw new BusinessRuleException("Department name already exists.");
        }

        var department = new Department
        {
            Name = name,
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _departments.Add(department);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(department);
    }

    public async Task<DepartmentDto?> UpdateAsync(int id, UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _departments.GetActiveForUpdateAsync(id, cancellationToken);
        if (department is null)
        {
            return null;
        }

        var name = request.Name.Trim();
        if (await _departments.NameTakenAsync(name, id, cancellationToken))
        {
            throw new BusinessRuleException("Department name already exists.");
        }

        department.Name = name;
        department.Description = request.Description?.Trim();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(department);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var department = await _departments.GetActiveForUpdateAsync(id, cancellationToken);
        if (department is null)
        {
            return false;
        }

        if (await _departments.HasMembersAsync(id, cancellationToken))
        {
            throw new BusinessRuleException("Department cannot be deleted because it still has members. Remove them first.");
        }

        // The department is only flagged, so the manager links to it would not cascade away on their own.
        _departments.RemoveManagerLinks(await _departments.GetManagerLinksForUpdateAsync(id, cancellationToken));

        department.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<DepartmentMembersDto?> GetMembersAsync(int departmentId, CancellationToken cancellationToken)
    {
        if (!await _departments.ExistsActiveAsync(departmentId, cancellationToken))
        {
            return null;
        }

        var managers = await _departments.GetManagersAsync(departmentId, cancellationToken);
        var employees = await _departments.GetEmployeesAsync(departmentId, cancellationToken);
        return new DepartmentMembersDto(departmentId, managers, employees);
    }

    public async Task<DepartmentMembersDto?> ReplaceMembersAsync(int departmentId, UpdateDepartmentMembersRequest request, CancellationToken cancellationToken)
    {
        if (await _departments.GetActiveForUpdateAsync(departmentId, cancellationToken) is null)
        {
            return null;
        }

        var managerIds = request.ManagerIds.Distinct().ToList();
        var employeeIds = request.EmployeeIds.Distinct().ToList();

        // Every manager must be an active Manager; every employee an active, non-deleted user.
        // One bad id rejects the whole request and leaves membership unchanged.
        var managerById = await _employees.GetByIdsAsync(managerIds, cancellationToken);
        foreach (var managerId in managerIds)
        {
            if (!managerById.TryGetValue(managerId, out var user)
                || !user.IsActive
                || user.RoleId != SystemRoles.ManagerId)
            {
                throw new BusinessRuleException($"User {managerId} is not an active manager.");
            }
        }

        var employeeById = await _employees.GetByIdsAsync(employeeIds, cancellationToken);
        foreach (var employeeId in employeeIds)
        {
            if (!employeeById.TryGetValue(employeeId, out var user) || !user.IsActive)
            {
                throw new BusinessRuleException($"User {employeeId} is not an active user.");
            }
        }

        var existingLinks = await _departments.GetManagerLinksForUpdateAsync(departmentId, cancellationToken);
        _departments.RemoveManagerLinks(existingLinks.Where(link => !managerIds.Contains(link.ManagerUserId)).ToList());
        foreach (var managerId in managerIds.Where(id => existingLinks.All(link => link.ManagerUserId != id)))
        {
            _departments.AddManagerLink(new DepartmentManager { DepartmentId = departmentId, ManagerUserId = managerId });
        }

        // A person may belong to several departments, so only this department's membership rows change.
        var existingMemberships = await _departments.GetMembershipsForUpdateAsync(departmentId, cancellationToken);
        _departments.RemoveMemberships(existingMemberships.Where(membership => !employeeIds.Contains(membership.UserId)).ToList());
        foreach (var employeeId in employeeIds.Where(id => existingMemberships.All(membership => membership.UserId != id)))
        {
            _departments.AddMembership(new UserDepartment { DepartmentId = departmentId, UserId = employeeId });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetMembersAsync(departmentId, cancellationToken);
    }

    private static DepartmentDto ToDto(Department department) =>
        new(department.Id, department.Name, department.Description, department.CreatedAt);
}
