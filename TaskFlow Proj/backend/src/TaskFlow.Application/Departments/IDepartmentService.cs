using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Departments;

public interface IDepartmentService
{
    Task<IReadOnlyCollection<DepartmentWithCountDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<DepartmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Domain.Entities.User>> GetEmployeesAsync(int departmentId, CancellationToken cancellationToken);
    Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken);
    Task<DepartmentDto?> UpdateAsync(int id, UpdateDepartmentRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
