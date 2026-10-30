using TaskFlow.Api.Repositories.Interfaces.Dashboard.Analytics;
using TaskFlow.Api.Dtos.Dashboard.Analytics;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Rules;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;

namespace TaskFlow.Api.Repositories.Dashboard.Analytics;

internal sealed record KeyCount(int Key, int Count);

/// <summary>Bucket counting shared by the Admin and Manager analytics so both use one definition of every bucket.</summary>
internal static class AnalyticsCounting
{
    public static async Task<BucketCountsDto> TotalsAsync(
        IQueryable<TaskEntity> tasks,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var counts = new int[Enum.GetValues<TaskBucket>().Length];
        foreach (var bucket in Enum.GetValues<TaskBucket>())
        {
            counts[(int)bucket] = await tasks.CountAsync(TaskBucketRules.InBucket(bucket, now), cancellationToken);
        }

        return ToBuckets(counts);
    }

    public static BucketCountsDto ToBuckets(int[] counts) => new(
        counts[(int)TaskBucket.Pending],
        counts[(int)TaskBucket.InProgress],
        counts[(int)TaskBucket.Completed],
        counts[(int)TaskBucket.Overdue]);

    public static async Task<Dictionary<int, BucketCountsDto>> CountByAsync(
        IQueryable<TaskEntity> tasks,
        DateTime now,
        Func<IQueryable<TaskEntity>, IQueryable<KeyCount>> group,
        CancellationToken cancellationToken)
    {
        var perKey = new Dictionary<int, int[]>();
        foreach (var bucket in Enum.GetValues<TaskBucket>())
        {
            var rows = await group(tasks.Where(TaskBucketRules.InBucket(bucket, now))).ToListAsync(cancellationToken);
            foreach (var row in rows)
            {
                if (!perKey.TryGetValue(row.Key, out var counts))
                {
                    counts = new int[Enum.GetValues<TaskBucket>().Length];
                    perKey[row.Key] = counts;
                }

                counts[(int)bucket] = row.Count;
            }
        }

        return perKey.ToDictionary(pair => pair.Key, pair => ToBuckets(pair.Value));
    }

    /// <summary>Counts instants per quarter hour so a caller can bucket them into local days in any timezone.</summary>
    public static async Task<List<QuarterHourCount>> QuarterHourCountsAsync(
        IQueryable<DateTime> instants,
        CancellationToken cancellationToken)
    {
        var rows = await instants
            .GroupBy(instant => new { instant.Year, instant.Month, instant.Day, instant.Hour, Quarter = instant.Minute / 15 })
            .Select(group => new { group.Key.Year, group.Key.Month, group.Key.Day, group.Key.Hour, group.Key.Quarter, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new QuarterHourCount(
                new DateTime(row.Year, row.Month, row.Day, row.Hour, row.Quarter * 15, 0, DateTimeKind.Utc),
                row.Count))
            .ToList();
    }

    public static async Task<IReadOnlyList<PriorityCountDto>> ByPriorityAsync(
        IQueryable<TaskEntity> tasks,
        CancellationToken cancellationToken)
    {
        var rows = await tasks
            .GroupBy(task => task.Priority)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return Enum.GetValues<TaskPriority>()
            .Select(priority => new PriorityCountDto(
                priority.ToString(),
                rows.Where(row => row.Key == priority).Sum(row => row.Count)))
            .ToList();
    }
}
