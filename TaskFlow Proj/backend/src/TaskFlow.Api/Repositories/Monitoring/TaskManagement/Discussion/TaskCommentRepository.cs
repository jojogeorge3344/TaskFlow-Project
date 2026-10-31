using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Monitoring.TaskManagement.Discussion;

public class TaskCommentRepository : BaseRepository<TaskComment>, ITaskCommentRepository
{
    public TaskCommentRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<TaskComment>> GetForTaskAsync(int taskId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .IgnoreQueryFilters() // Users are soft-deleted behind a query filter; history must keep showing a deleted person's name.
            .Include(comment => comment.Author)
            .Where(comment => comment.TaskId == taskId && !comment.IsDeleted)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToListAsync(cancellationToken);

    public Task<TaskComment?> GetLiveForUpdateAsync(int taskId, int commentId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(candidate => candidate.Id == commentId && candidate.TaskId == taskId && !candidate.IsDeleted, cancellationToken);
}
