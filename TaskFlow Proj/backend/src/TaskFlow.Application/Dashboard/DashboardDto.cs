namespace TaskFlow.Application.Dashboard;

public record AdminDashboardDto(
    int TotalEmployees,
    int ActiveEmployees,
    int TotalDepartments,
    int TotalTasks,
    int PendingTasks,
    int CompletedTasks,
    int OverdueTasks);

public record ManagerDashboardDto(
    int TotalTasks,
    int PendingTasks,
    int TasksDueToday,
    int UpcomingDeadlines,
    int OverdueTasks,
    int CompletedTasks);

public record EmployeeDashboardDto(
    int AssignedTasks,
    int PendingTasks,
    int TasksDueToday,
    int OverdueTasks,
    int InProgressTasks,
    int CompletedTasks);
