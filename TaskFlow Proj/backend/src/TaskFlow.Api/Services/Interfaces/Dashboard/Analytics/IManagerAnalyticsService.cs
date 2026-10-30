using TaskFlow.Api.Dtos.Dashboard.Analytics;

namespace TaskFlow.Api.Services.Interfaces.Dashboard.Analytics;

public interface IManagerAnalyticsService
{
    Task<ManagerAnalyticsResponseDto> GetAsync(int managerId, AnalyticsFilter filter, CancellationToken cancellationToken);

    Task<ManagerFilterOptionsDto> GetFilterOptionsAsync(int managerId, CancellationToken cancellationToken);
}
