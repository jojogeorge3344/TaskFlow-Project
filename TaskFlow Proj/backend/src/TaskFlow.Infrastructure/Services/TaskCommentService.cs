using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Task discussion comments. Comments are immutable - created once, never edited or deleted.</summary>
public class TaskCommentService : ITaskCommentService
{
    private readonly AppDbContext _dbContext;
    private readonly ITaskActivityService _activityService;
    private readonly TimeProvider _timeProvider;

    public TaskCommentService(
        AppDbContext dbContext,
        ITaskActivityService activityService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _activityService = activityService;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyCollection<TaskCommentDto>> GetForTaskAsync(
        int taskId,
        CancellationToken cancellationToken)
    {
        var comments = await _dbContext.TaskComments
            .AsNoTracking()
            .Include(comment => comment.Author)
            .Where(comment => comment.TaskId == taskId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(cancellationToken);

        return comments
            .Select(comment => new TaskCommentDto(
                comment.Id,
                comment.Body,
                comment.AuthorUserId,
                comment.Author?.FullName ?? string.Empty,
                comment.CreatedAt))
            .ToList();
    }

    public async Task<TaskCommentDto> AddAsync(
        int taskId,
        AddCommentRequest request,
        int authorUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new InvalidOperationException("Comment must not be empty.");
        }

        var taskExists = await _dbContext.Tasks
            .AnyAsync(task => task.Id == taskId && !task.IsDeleted, cancellationToken);

        if (!taskExists)
        {
            throw new InvalidOperationException("Task not found.");
        }

        var comment = new TaskComment
        {
            TaskId = taskId,
            AuthorUserId = authorUserId,
            Body = request.Body.Trim(),
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.TaskComments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _activityService.AppendAsync(
            taskId,
            Domain.Enums.TaskActivityEventType.CommentAdded,
            authorUserId,
            "New comment added",
            cancellationToken: cancellationToken);

        var authorName = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == authorUserId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new TaskCommentDto(comment.Id, comment.Body, comment.AuthorUserId, authorName, comment.CreatedAt);
    }
}