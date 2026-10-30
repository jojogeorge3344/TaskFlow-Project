namespace TaskFlow.Api.Models.Entities;

/// <summary>An immutable comment on a task. Created by TaskCommentService; edited or soft-deleted only by its author.</summary>
public class TaskComment
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public TaskFlow.Api.Models.Entities.Task? Task { get; set; }
    public int AuthorUserId { get; set; }
    public User? Author { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
}