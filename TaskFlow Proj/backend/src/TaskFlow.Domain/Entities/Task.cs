using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class Task
{
    public int Id { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int AssignedToUserId { get; set; }
    public User? AssignedTo { get; set; }
    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime Deadline { get; set; }
    public decimal EstimatedHours { get; set; }
    public Enums.TaskStatus Status { get; set; }
    public int FirstReminderDays { get; set; } = 2;
    public int SecondReminderDays { get; set; } = 1;
    public ReminderFrequency OverdueReminderFrequency { get; set; } = ReminderFrequency.Daily;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool IsDeleted { get; set; }
}
