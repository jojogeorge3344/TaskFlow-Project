namespace TaskFlow.Domain.Entities;

/// <summary>An immutable comment on a task. Created by TaskCommentService; never updated or deleted.</summary>
public class TaskComment
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public Domain.Entities.Task? Task { get; set; }
    public int AuthorUserId { get; set; }
    public User? Author { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}