namespace TaskFlow.Application.Dashboard.Analytics;

public interface IAdminAnalyticsService
{
    Task<AdminAnalyticsDto> GetAsync(AnalyticsFilter filter, CancellationToken cancellationToken);

    Task<AnalyticsFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken);
}
