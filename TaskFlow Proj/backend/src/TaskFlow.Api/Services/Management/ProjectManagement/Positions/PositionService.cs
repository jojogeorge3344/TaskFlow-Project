using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Management.ProjectManagement.Positions;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Repositories.Management.ProjectManagement.Positions;

namespace TaskFlow.Api.Services.Management.ProjectManagement.Positions;

public class PositionService : IPositionService
{
    private const int MaxNameLength = 100;

    private readonly IPositionRepository _positions;
    private readonly IUnitOfWork _unitOfWork;

    public PositionService(IPositionRepository positions, IUnitOfWork unitOfWork)
    {
        _positions = positions;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyCollection<PositionDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await _positions.GetAllAsync(cancellationToken);

    public async Task<PositionDto> GetOrCreateAsync(string name, CancellationToken cancellationToken)
    {
        var trimmed = string.Join(' ', (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (trimmed.Length == 0)
        {
            throw new BusinessRuleException("Position name is required.");
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw new BusinessRuleException($"Position name must be {MaxNameLength} characters or fewer.");
        }

        // The column is NOCASE, so this matches "qa" to "QA".
        var existing = await _positions.GetByNameAsync(trimmed, cancellationToken);
        if (existing is not null)
        {
            return new PositionDto(existing.Id, existing.Name);
        }

        var position = new Position { Name = trimmed };
        _positions.Add(position);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new PositionDto(position.Id, position.Name);
    }
}
