using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Application.Mail;
using TaskFlow.Application.Settings;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Task discussion comments. Only a comment's author may edit or (soft) delete it; every add, edit
/// and delete is logged in the task's activity and notifies the task's other assigned members.</summary>
public class TaskCommentService : ITaskCommentService
{
    private const int ExcerptLength = 200;

    private readonly AppDbContext _dbContext;
    private readonly ITaskActivityService _activityService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TaskCommentService> _logger;

    public TaskCommentService(
        AppDbContext dbContext,
        ITaskActivityService activityService,
        INotificationPublisher notificationPublisher,
        TimeProvider timeProvider,
        ILogger<TaskCommentService>? logger = null)
    {
        _dbContext = dbContext;
        _activityService = activityService;
        _notificationPublisher = notificationPublisher;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<TaskCommentService>.Instance;
    }

    public async Task<IReadOnlyCollection<TaskCommentDto>> GetForTaskAsync(
        int taskId,
        CancellationToken cancellationToken)
    {
        var comments = await _dbContext.TaskComments
            .AsNoTracking()
            .IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Include(comment => comment.Author)
            .Where(comment => comment.TaskId == taskId && !comment.IsDeleted)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToListAsync(cancellationToken);

        return comments
            .Select(comment => ToDto(comment, comment.Author?.FullName ?? string.Empty))
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

        var task = await FindTaskAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException("Task not found.");

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
            TaskActivityEventType.CommentAdded,
            authorUserId,
            "New comment added",
            newValue: Excerpt(comment.Body),
            cancellationToken: cancellationToken);

        var authorName = await GetUserNameAsync(authorUserId, cancellationToken);
        await NotifyMembersAsync(task, authorUserId, authorName, "added", $"\"{Excerpt(comment.Body)}\"", $"Comment:{Environment.NewLine}{Excerpt(comment.Body)}", cancellationToken);

        return ToDto(comment, authorName);
    }

    public async Task<TaskCommentDto?> UpdateAsync(
        int taskId,
        int commentId,
        AddCommentRequest request,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new InvalidOperationException("Comment must not be empty.");
        }

        var task = await FindTaskAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException("Task not found.");

        var comment = await _dbContext.TaskComments
            .FirstOrDefaultAsync(candidate => candidate.Id == commentId && candidate.TaskId == taskId && !candidate.IsDeleted, cancellationToken);
        if (comment is null)
        {
            return null;
        }

        if (comment.AuthorUserId != actorUserId)
        {
            throw new UnauthorizedAccessException("Only the author can edit this comment.");
        }

        var oldBody = comment.Body;
        comment.Body = request.Body.Trim();
        comment.EditedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _activityService.AppendAsync(
            taskId,
            TaskActivityEventType.CommentEdited,
            actorUserId,
            "Comment edited",
            Excerpt(oldBody),
            Excerpt(comment.Body),
            cancellationToken);

        var authorName = await GetUserNameAsync(actorUserId, cancellationToken);
        await NotifyMembersAsync(task, actorUserId, authorName, "edited", $"\"{Excerpt(oldBody)}\" → \"{Excerpt(comment.Body)}\"", $"Previous:{Environment.NewLine}{Excerpt(oldBody)}{Environment.NewLine}{Environment.NewLine}Updated:{Environment.NewLine}{Excerpt(comment.Body)}", cancellationToken);

        return ToDto(comment, authorName);
    }

    public async Task<bool> DeleteAsync(
        int taskId,
        int commentId,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        var task = await FindTaskAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException("Task not found.");

        var comment = await _dbContext.TaskComments
            .FirstOrDefaultAsync(candidate => candidate.Id == commentId && candidate.TaskId == taskId && !candidate.IsDeleted, cancellationToken);
        if (comment is null)
        {
            return false;
        }

        if (comment.AuthorUserId != actorUserId)
        {
            throw new UnauthorizedAccessException("Only the author can delete this comment.");
        }

        comment.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _activityService.AppendAsync(
            taskId,
            TaskActivityEventType.CommentDeleted,
            actorUserId,
            "Comment deleted",
            oldValue: Excerpt(comment.Body),
            cancellationToken: cancellationToken);

        var authorName = await GetUserNameAsync(actorUserId, cancellationToken);
        await NotifyMembersAsync(task, actorUserId, authorName, "deleted", $"\"{Excerpt(comment.Body)}\"", $"Deleted comment:{Environment.NewLine}{Excerpt(comment.Body)}", cancellationToken);

        return true;
    }

    private static string Excerpt(string body) =>
        body.Length > ExcerptLength ? body[..ExcerptLength] + "…" : body;

    private static TaskCommentDto ToDto(TaskComment comment, string authorName) =>
        new(comment.Id, comment.Body, comment.AuthorUserId, authorName, comment.CreatedAt, comment.EditedAt);

    private Task<Domain.Entities.Task?> FindTaskAsync(int taskId, CancellationToken cancellationToken) =>
        _dbContext.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(task => task.Id == taskId && !task.IsDeleted, cancellationToken);

    private async Task<string> GetUserNameAsync(int userId, CancellationToken cancellationToken) =>
        await _dbContext.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(user => user.Id == userId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

    /// <summary>Tells the task's assigned members (the assignee, every additional recipient and the
    /// person who made the change) by in-app notification and Inbox mail. A delivery failure is logged and never
    /// undoes the comment change that has already been saved.</summary>
    private async System.Threading.Tasks.Task NotifyMembersAsync(
        Domain.Entities.Task task,
        int actorUserId,
        string actorName,
        string verb,
        string notificationDetail,
        string mailDetail,
        CancellationToken cancellationToken)
    {
        try
        {
            var memberIds = await _dbContext.TaskRecipients
                .AsNoTracking()
                .Where(recipient => recipient.TaskId == task.Id)
                .Select(recipient => recipient.UserId)
                .ToListAsync(cancellationToken);
            memberIds.Add(task.AssignedToUserId);
            memberIds.Add(actorUserId); // the person who made the change is told too

            // The Users query filter hides soft-deleted people, so they are never notified.
            var targetIds = await _dbContext.Users
                .AsNoTracking()
                .Where(user => memberIds.Contains(user.Id))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);
            if (targetIds.Count == 0)
            {
                return;
            }

            var who = string.IsNullOrWhiteSpace(actorName) ? "Someone" : actorName;
            var message = $"{who} {verb} a comment on \"{task.Title}\": {notificationDetail}";

            foreach (var userId in targetIds)
            {
                await _notificationPublisher.PublishAsync(userId, task.Id, NotificationType.TaskComment, message, cancellationToken);
            }

            if (await MailEnabledAsync(cancellationToken))
            {
                AddInboxMail(task, targetIds, who, verb, mailDetail);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _dbContext.ChangeTracker.Clear();
            _logger.LogWarning(exception, "Could not notify members of task {TaskId} about a discussion change.", task.Id);
        }
    }

    private void AddInboxMail(Domain.Entities.Task task, IReadOnlyCollection<int> recipientIds, string who, string verb, string mailDetail)
    {
        var subject = $"Comment {verb}: {task.Title}";
        if (subject.Length > MailRules.MaxSubjectLength)
        {
            subject = subject[..MailRules.MaxSubjectLength];
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var mail = new MailMessage
        {
            ConversationId = Guid.NewGuid(),
            IsSystem = true,
            Subject = subject,
            Body = $"{who} {verb} a comment on the task \"{task.Title}\" ({task.TaskCode}).{Environment.NewLine}{Environment.NewLine}{mailDetail}{Environment.NewLine}{Environment.NewLine}This is an automatic message from TaskFlow.",
            TaskId = task.Id,
            CreatedAt = now,
            UpdatedAt = now,
            SentAt = now
        };
        _dbContext.MailMessages.Add(mail);

        foreach (var userId in recipientIds)
        {
            _dbContext.MailRecipients.Add(new MailRecipient { Message = mail, UserId = userId, Kind = MailRecipientKind.To });
            _dbContext.MailboxItems.Add(new MailboxItem { Message = mail, OwnerUserId = userId, Folder = MailFolder.Inbox, SortAt = now });
        }
    }

    /// <summary>Defaults to enabled when the row is missing or unreadable, like the in-app preference.</summary>
    private async Task<bool> MailEnabledAsync(CancellationToken cancellationToken)
    {
        var raw = await _dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(setting => setting.Key == SettingKeys.NotificationMailEnabled)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return raw is null || !bool.TryParse(raw, out var enabled) || enabled;
    }
}
