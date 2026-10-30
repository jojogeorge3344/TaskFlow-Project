using TaskFlow.Api.Dtos.Management.ProjectManagement.Positions;
using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Repositories.Interfaces.Management.ProjectManagement.Positions;

/// <summary>The job positions people hold on projects.</summary>
public interface IPositionRepository : IBaseRepository<Position>
{
    /// <summary>Every position, by name.</summary>
    Task<IReadOnlyList<PositionDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The position with this name, ignoring case (the column is NOCASE).</summary>
    Task<Position?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>How many of these ids are positions.</summary>
    Task<int> CountAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);
}
