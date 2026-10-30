using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Interfaces.Dashboard;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.Api.Repositories.Dashboard;

public class DashboardRepository : BaseRepository<TaskEntity>, IDashboardRepository
{
    public DashboardRepository(AppDbContext db)
        : base(db)
    {
    }

    public Task<int> CountUsersAsync(bool activeOnly, CancellationToken cancellationToken = default) =>
        Db.Users.CountAsync(user => !activeOnly || user.IsActive, cancellationToken);

    public Task<int> CountProjectsAsync(CancellationToken cancellationToken = default) =>
        Db.Projects.CountAsync(cancellationToken);

    public async Task<DashboardTaskCounts> CountTasksAsync(
        DashboardTaskScope scope,
        DashboardDayBounds bounds,
        CancellationToken cancellationToken = default)
    {
        var tasks = Set.AsNoTracking().Where(task => !task.IsDeleted);

        if (scope.TeamUserIds is { } team)
        {
            tasks = tasks.Where(task =>
                team.Contains(task.AssignedToUserId)
                || Db.TaskRecipients.Any(recipient => recipient.TaskId == task.Id && team.Contains(recipient.UserId)));
        }

        if (scope.OwnUserId is int own)
        {
            tasks = tasks.Where(task =>
                task.AssignedToUserId == own
                || Db.TaskRecipients.Any(recipient => recipient.TaskId == task.Id && recipient.UserId == own));
        }

        var open = tasks.Where(task => task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled);

        return new DashboardTaskCounts(
            await tasks.CountAsync(cancellationToken),
            await open.CountAsync(cancellationToken),
            await open.CountAsync(task => task.Deadline >= bounds.TodayStart && task.Deadline < bounds.TomorrowStart, cancellationToken),
            await open.CountAsync(task => task.Deadline >= bounds.TomorrowStart && task.Deadline < bounds.UpcomingHorizon, cancellationToken),
            await open.CountAsync(task => task.Deadline < bounds.Now, cancellationToken),
            await tasks.CountAsync(task => task.Status == TaskStatus.InProgress || task.Status == TaskStatus.InQA, cancellationToken),
            await tasks.CountAsync(task => task.Status == TaskStatus.Completed, cancellationToken));
    }
}
