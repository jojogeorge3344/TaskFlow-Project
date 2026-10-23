namespace TaskFlow.Application.Dashboard.Analytics;

public interface IEmployeeAnalyticsService
{
    /// <summary>Analytics of the signed-in user's own tasks. Throws <see cref="AnalyticsFilterException"/> for an unknown period.</summary>
    Task<EmployeeAnalyticsResponseDto> GetAsync(int userId, string? period, CancellationToken cancellationToken);
}
