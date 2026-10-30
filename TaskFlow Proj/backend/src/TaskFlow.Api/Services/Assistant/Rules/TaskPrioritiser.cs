using TaskFlow.Api.Dtos.Monitoring.TaskManagement;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Rules;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

using TaskFlow.Api.Dtos.Assistant;

namespace TaskFlow.Api.Services.Assistant.Rules;

/// <summary>Orders a user's open tasks. The order is computed here, from the data only, so it is identical whichever
/// AI model is asked to explain it: overdue first (longest overdue first), then due today, then the nearest
/// deadline, with higher priority first inside the same day. Completed and cancelled tasks are left out; blocked
/// tasks stay in the list and are flagged.</summary>
public static class TaskPrioritiser
{
    public static IReadOnlyList<PrioritisedTask> Rank(IEnumerable<TaskListItemDto> tasks, DateTime utcNow, TimeZoneInfo timeZone)
    {
        var today = ReminderCalculator.LocalDate(utcNow, timeZone);

        return tasks
            .Where(task => task.Status is not (TaskStatus.Completed or TaskStatus.Cancelled))
            .Select(task => new
            {
                Task = task,
                Stage = TaskStageCalculator.Calculate(task.Status, task.Deadline, utcNow, timeZone),
                DueDate = ReminderCalculator.LocalDate(task.Deadline, timeZone)
            })
            .OrderBy(item => StageOrder(item.Stage))
            .ThenBy(item => item.DueDate)
            .ThenByDescending(item => item.Task.Priority)
            .ThenBy(item => item.Task.Deadline)
            .ThenBy(item => item.Task.TaskCode, StringComparer.Ordinal)
            .Select((item, index) => new PrioritisedTask(
                index + 1,
                item.Task.Id,
                item.Task.TaskCode,
                item.Task.Title,
                item.Task.Priority,
                item.Task.Status,
                item.Task.Deadline,
                item.DueDate,
                item.DueDate.DayNumber - today.DayNumber,
                item.Stage,
                item.Task.Status == TaskStatus.Blocked))
            .ToList();
    }

    private static int StageOrder(TaskStage stage) => stage switch
    {
        TaskStage.Overdue => 0,
        TaskStage.DueToday => 1,
        _ => 2
    };
}
