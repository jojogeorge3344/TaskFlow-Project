using TaskFlow.Api.Common.Rules;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class TaskBucketRulesTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private static TaskEntity TaskWith(TaskStatus status, DateTime deadline) =>
        new() { Status = status, Deadline = deadline };

    [Theory]
    [InlineData(TaskStatus.ToDo, 1, TaskBucket.Pending)]
    [InlineData(TaskStatus.Blocked, 1, TaskBucket.Pending)]
    [InlineData(TaskStatus.InProgress, 1, TaskBucket.InProgress)]
    [InlineData(TaskStatus.InQA, 1, TaskBucket.InProgress)]
    [InlineData(TaskStatus.Completed, 1, TaskBucket.Completed)]
    [InlineData(TaskStatus.ToDo, -1, TaskBucket.Overdue)]
    [InlineData(TaskStatus.Blocked, -1, TaskBucket.Overdue)]
    [InlineData(TaskStatus.InProgress, -1, TaskBucket.Overdue)]
    [InlineData(TaskStatus.InQA, -1, TaskBucket.Overdue)]
    [InlineData(TaskStatus.Completed, -1, TaskBucket.Completed)]
    public void Classify_PutsEachTaskInExactlyOneBucket(TaskStatus status, int deadlineOffsetDays, TaskBucket expected)
    {
        var task = TaskWith(status, Now.AddDays(deadlineOffsetDays));

        Assert.Equal(expected, TaskBucketRules.Classify(task, Now));
    }

    [Fact]
    public void InBucket_MatchesClassifyForEveryStatusAndDeadline()
    {
        foreach (var status in Enum.GetValues<TaskStatus>().Where(status => status != TaskStatus.Cancelled))
        {
            foreach (var offset in new[] { -2, 2 })
            {
                var task = TaskWith(status, Now.AddDays(offset));
                var expected = TaskBucketRules.Classify(task, Now);

                foreach (var bucket in Enum.GetValues<TaskBucket>())
                {
                    var matches = TaskBucketRules.InBucket(bucket, Now).Compile()(task);
                    Assert.Equal(bucket == expected, matches);
                }
            }
        }
    }

    [Fact]
    public void InBucket_ExcludesCancelledAndDeletedTasks()
    {
        var cancelled = TaskWith(TaskStatus.Cancelled, Now.AddDays(2));
        var deleted = TaskWith(TaskStatus.ToDo, Now.AddDays(2));
        deleted.IsDeleted = true;

        foreach (var bucket in Enum.GetValues<TaskBucket>())
        {
            Assert.False(TaskBucketRules.InBucket(bucket, Now).Compile()(cancelled));
            Assert.False(TaskBucketRules.InBucket(bucket, Now).Compile()(deleted));
        }
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 5, 0)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1, 8, 13)]
    [InlineData(5, 5, 100)]
    public void CompletionPercent_IsWholeNumberAndZeroWhenEmpty(int completed, int total, int expected)
    {
        Assert.Equal(expected, TaskBucketRules.CompletionPercent(completed, total));
    }
}
