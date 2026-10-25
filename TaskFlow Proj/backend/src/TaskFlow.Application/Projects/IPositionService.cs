namespace TaskFlow.Application.Projects;

public interface IPositionService
{
    Task<IReadOnlyCollection<PositionDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Returns the position with this name (ignoring case) or creates it. Throws
    /// <see cref="InvalidOperationException"/> for a blank or over-long name.</summary>
    Task<PositionDto> GetOrCreateAsync(string name, CancellationToken cancellationToken);
}
