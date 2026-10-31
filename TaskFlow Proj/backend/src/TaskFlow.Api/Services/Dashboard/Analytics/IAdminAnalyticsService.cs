using TaskFlow.Api.Dtos.Dashboard.Analytics;

namespace TaskFlow.Api.Services.Dashboard.Analytics;

public interface IAdminAnalyticsService
{
    Task<AdminAnalyticsDto> GetAsync(AnalyticsFilter filter, CancellationToken cancellationToken);

    Task<AnalyticsFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken);
}
