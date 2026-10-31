using System.Linq.Expressions;
using TaskFlow.Api.Models.Enums;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Common.Rules;

public enum TaskBucket
{
    Pending,
    InProgress,
    Completed,
    Overdue
}

public static class TaskBucketRules
{
    public static Expression<Func<TaskEntity, bool>> IsCounted() =>
        task => !task.IsDeleted && task.Status != TaskStatus.Cancelled;

    public static Expression<Func<TaskEntity, TaskBucket>> BucketOf(DateTime now) =>
        task => task.Status == TaskStatus.Completed
            ? TaskBucket.Completed
            : task.Deadline < now
                ? TaskBucket.Overdue
                : task.Status == TaskStatus.InProgress || task.Status == TaskStatus.InQA
                    ? TaskBucket.InProgress
                    : TaskBucket.Pending;

    public static Expression<Func<TaskEntity, bool>> InBucket(TaskBucket bucket, DateTime now)
    {
        var bucketOf = BucketOf(now);
        var isCounted = IsCounted();
        var parameter = bucketOf.Parameters[0];
        var counted = new ParameterReplacer(isCounted.Parameters[0], parameter).Visit(isCounted.Body);
        var body = Expression.AndAlso(counted, Expression.Equal(bucketOf.Body, Expression.Constant(bucket)));
        return Expression.Lambda<Func<TaskEntity, bool>>(body, parameter);
    }

    public static TaskBucket Classify(TaskEntity task, DateTime now) => BucketOf(now).Compile()(task);

    public static int CompletionPercent(int completed, int total) =>
        total <= 0 ? 0 : (int)Math.Round(completed * 100.0 / total, MidpointRounding.AwayFromZero);

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
