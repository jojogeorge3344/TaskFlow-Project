using TaskFlow.Domain.Enums;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Domain.Rules;

/// <summary>The display stage of a task, derived at read time from its status and deadline.
/// Never stored (agent.md: overdue is never stored).</summary>
public enum TaskStage
{
    Upcoming,
    DueToday,
    Overdue,
    Completed
}

public static class TaskStageCalculator
{
    public static TaskStage Calculate(TaskStatus status, DateTime deadline, DateTime utcNow)
    {
        if (status is TaskStatus.Completed or TaskStatus.Cancelled)
        {
            return TaskStage.Completed;
        }

        if (deadline.Date < utcNow.Date)
        {
            return TaskStage.Overdue;
        }

        if (deadline.Date == utcNow.Date)
        {
            return TaskStage.DueToday;
        }

        return TaskStage.Upcoming;
    }
}