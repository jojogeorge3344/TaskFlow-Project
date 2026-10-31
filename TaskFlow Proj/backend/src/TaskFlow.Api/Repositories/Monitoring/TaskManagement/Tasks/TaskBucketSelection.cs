using TaskFlow.Api.Common.Rules;

namespace TaskFlow.Api.Repositories.Monitoring.TaskManagement.Tasks;

/// <summary>A dashboard bucket to filter by: every counted task, or one bucket as of <see cref="Now"/>.</summary>
public record TaskBucketSelection(TaskBucket? Bucket, DateTime Now)
{
    public bool All => Bucket is null;
}
