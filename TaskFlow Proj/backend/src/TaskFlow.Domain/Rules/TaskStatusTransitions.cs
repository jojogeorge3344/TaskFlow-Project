using TaskFlow.Domain.Enums;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Domain.Rules;

/// <summary>Pure transition rules per TDD §10.2 and Appendix A. Completed and Cancelled are
/// terminal - no transitions out. Repeating the same status is always allowed.</summary>
public static class TaskStatusTransitions
{
    public static bool Can(TaskStatus from, TaskStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return from switch
        {
            TaskStatus.ToDo => to is TaskStatus.InProgress or TaskStatus.Blocked or TaskStatus.Cancelled,
            TaskStatus.InProgress => to is TaskStatus.Blocked or TaskStatus.Completed or TaskStatus.Cancelled,
            TaskStatus.Blocked => to is TaskStatus.InProgress or TaskStatus.Cancelled,
            _ => false
        };
    }
}