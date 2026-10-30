using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Projects;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Infrastructure.Services;

public class PositionService : IPositionService
{
    private const int MaxNameLength = 100;

    private readonly AppDbContext _dbContext;

    public PositionService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PositionDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await _dbContext.Positions
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PositionDto(p.Id, p.Name))
            .ToListAsync(cancellationToken);

    public async Task<PositionDto> GetOrCreateAsync(string name, CancellationToken cancellationToken)
    {
        var trimmed = string.Join(' ', (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException("Position name is required.");
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw new InvalidOperationException($"Position name must be {MaxNameLength} characters or fewer.");
        }

        // The column is NOCASE, so this matches "qa" to "QA".
        var existing = await _dbContext.Positions.FirstOrDefaultAsync(p => p.Name == trimmed, cancellationToken);
        if (existing is not null)
        {
            return new PositionDto(existing.Id, existing.Name);
        }

        var position = new Position { Name = trimmed };
        _dbContext.Positions.Add(position);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new PositionDto(position.Id, position.Name);
    }
}
