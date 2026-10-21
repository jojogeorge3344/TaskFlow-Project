using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Projects;

public interface IProjectService
{
    Task<IReadOnlyCollection<ProjectWithCountDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<ProjectDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Domain.Entities.User>> GetEmployeesAsync(int projectId, CancellationToken cancellationToken);
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);
    Task<ProjectDto?> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
