namespace TaskFlow.Api.Dtos.Monitoring.TaskManagement;

/// <summary>A member of a project's team who can be assigned a new task (Screen 8), with their current load. Active and
/// overdue use the Employee Workload screen's rules and count tasks across every project. OnTimeRate is the share (0 to 1)
/// of their completed tasks finished by the deadline, or null when they have completed none.</summary>
public record AssigneeCandidateDto(
    int UserId,
    string FullName,
    string? EmployeeCode,
    int ActiveTasks,
    int OverdueTasks,
    double? OnTimeRate);
