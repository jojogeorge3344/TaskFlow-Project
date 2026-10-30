using TaskFlow.Api.Models.Enums;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Common.Rules;

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
            TaskStatus.InProgress => to is TaskStatus.InQA or TaskStatus.Blocked or TaskStatus.Completed or TaskStatus.Cancelled,
            TaskStatus.Blocked => to is TaskStatus.InProgress or TaskStatus.Cancelled,
            TaskStatus.InQA => to is TaskStatus.InProgress or TaskStatus.Completed or TaskStatus.Blocked or TaskStatus.Cancelled,
            _ => false
        };
    }
}