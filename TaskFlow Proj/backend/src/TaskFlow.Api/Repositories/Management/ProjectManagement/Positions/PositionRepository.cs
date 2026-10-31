using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Dtos.Management.ProjectManagement.Positions;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Api.Repositories.Management.ProjectManagement.Positions;

public class PositionRepository : BaseRepository<Position>, IPositionRepository
{
    public PositionRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<PositionDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PositionDto(p.Id, p.Name))
            .ToListAsync(cancellationToken);

    public Task<Position?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(p => p.Name == name, cancellationToken);

    public Task<int> CountAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        Set.CountAsync(p => ids.Contains(p.Id), cancellationToken);
}
