using TaskFlow.Api.Dtos.Dashboard.Analytics;

namespace TaskFlow.Api.Services.Dashboard.Analytics.Admin;

/// <summary>Arithmetic on bucket counts shared by the Admin, Manager and Employee analytics.</summary>
internal static class AnalyticsBuckets
{
    public static readonly BucketCountsDto Empty = new(0, 0, 0, 0);

    public static BucketCountsDto Sum(IEnumerable<BucketCountsDto> parts) => parts.Aggregate(
        Empty,
        (total, part) => new BucketCountsDto(
            total.Pending + part.Pending,
            total.InProgress + part.InProgress,
            total.Completed + part.Completed,
            total.Overdue + part.Overdue));
}
