using TaskFlow.Api.Dtos.Management.DepartmentManagement;
using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Services.Management.DepartmentManagement;

public interface IDepartmentService
{
    Task<IReadOnlyCollection<DepartmentWithCountsDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<DepartmentDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken);
    Task<DepartmentDto?> UpdateAsync(int id, UpdateDepartmentRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<DepartmentMembersDto?> GetMembersAsync(int departmentId, CancellationToken cancellationToken);
    Task<DepartmentMembersDto?> ReplaceMembersAsync(int departmentId, UpdateDepartmentMembersRequest request, CancellationToken cancellationToken);
}
