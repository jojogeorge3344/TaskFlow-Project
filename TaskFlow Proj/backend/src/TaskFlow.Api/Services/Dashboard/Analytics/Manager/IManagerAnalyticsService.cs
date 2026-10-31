using TaskFlow.Api.Dtos.Dashboard.Analytics;

namespace TaskFlow.Api.Services.Dashboard.Analytics.Manager;

public interface IManagerAnalyticsService
{
    Task<ManagerAnalyticsResponseDto> GetAsync(int managerId, AnalyticsFilter filter, CancellationToken cancellationToken);

    Task<ManagerFilterOptionsDto> GetFilterOptionsAsync(int managerId, CancellationToken cancellationToken);
}
