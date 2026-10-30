using System.Linq.Expressions;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Domain.Rules;

public enum DeadlineBucket
{
    Overdue,
    DueToday,
    Next7Days,
    Later
}

public readonly record struct DeadlineBounds(DateTime NowUtc, DateTime TomorrowUtc, DateTime HorizonUtc);

public static class DeadlineBucketRules
{
    private const int UpcomingWindowDays = 7;

    public static DeadlineBounds BoundsFor(DateTime nowUtc, TimeZoneInfo timeZone)
    {
        var today = ReminderCalculator.LocalDate(nowUtc, timeZone);
        return new DeadlineBounds(
            nowUtc,
            ReminderCalculator.LocalMidnightUtc(today.AddDays(1), timeZone),
            ReminderCalculator.LocalMidnightUtc(today.AddDays(1 + UpcomingWindowDays), timeZone));
    }

    public static Expression<Func<TaskEntity, bool>> InBucket(DeadlineBucket bucket, DeadlineBounds bounds)
    {
        var now = bounds.NowUtc;
        var tomorrow = bounds.TomorrowUtc;
        var horizon = bounds.HorizonUtc;

        return bucket switch
        {
            DeadlineBucket.Overdue => task => !task.IsDeleted
                && task.Status != TaskStatus.Completed
                && task.Status != TaskStatus.Cancelled
                && task.Deadline < now,
            DeadlineBucket.DueToday => task => !task.IsDeleted
                && task.Status != TaskStatus.Completed
                && task.Status != TaskStatus.Cancelled
                && task.Deadline >= now
                && task.Deadline < tomorrow,
            DeadlineBucket.Next7Days => task => !task.IsDeleted
                && task.Status != TaskStatus.Completed
                && task.Status != TaskStatus.Cancelled
                && task.Deadline >= tomorrow
                && task.Deadline < horizon,
            _ => task => !task.IsDeleted
                && task.Status != TaskStatus.Completed
                && task.Status != TaskStatus.Cancelled
                && task.Deadline >= horizon
        };
    }
}
