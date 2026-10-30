using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Interfaces.Monitoring.TaskManagement;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Repositories.Monitoring.TaskManagement;

public class TaskRepository : BaseRepository<TaskFlow.Api.Models.Entities.Task>, ITaskRepository
{
    public TaskRepository(AppDbContext db)
        : base(db)
    {
    }

    public Task<int> CountUnfinishedAssignedToAsync(int userId, CancellationToken cancellationToken = default) =>
        Set.CountAsync(
            t => t.AssignedToUserId == userId
                 && !t.IsDeleted
                 && t.Status != TaskStatus.Completed
                 && t.Status != TaskStatus.Cancelled,
            cancellationToken);
}
