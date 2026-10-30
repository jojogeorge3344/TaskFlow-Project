namespace TaskFlow.Api.Dtos.Monitoring.TaskManagement.Discussion;

public record TaskCommentDto(int Id, string Body, int AuthorUserId, string AuthorName, DateTime CreatedAt, DateTime? EditedAt = null);

public record AddCommentRequest(string Body);
