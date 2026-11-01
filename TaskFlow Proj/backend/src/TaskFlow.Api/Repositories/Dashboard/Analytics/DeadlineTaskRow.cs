using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;

namespace TaskFlow.Api.Repositories.Dashboard.Analytics;

public record DeadlineTaskRow(int Id, string TaskCode, string Title, TaskPriority Priority, DateTime Deadline);
