using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Persistence;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Dashboard.Overview;

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

        var todayStart = bounds.TodayStart;
        var tomorrowStart = bounds.TomorrowStart;
        var upcomingHorizon = bounds.UpcomingHorizon;
        var now = bounds.Now;

        // One grouped query: each row contributes 1 to every count it falls in.
        var counts = await tasks
            .Select(task => new
            {
                Open = task.Status != TaskStatus.Completed && task.Status != TaskStatus.Cancelled ? 1 : 0,
                Deadline = task.Deadline,
                InProgress = task.Status == TaskStatus.InProgress || task.Status == TaskStatus.InQA ? 1 : 0,
                Completed = task.Status == TaskStatus.Completed ? 1 : 0
            })
            .GroupBy(_ => 1)
            .Select(group => new DashboardTaskCounts(
                group.Count(),
                group.Sum(row => row.Open),
                group.Sum(row => row.Open == 1 && row.Deadline >= todayStart && row.Deadline < tomorrowStart ? 1 : 0),
                group.Sum(row => row.Open == 1 && row.Deadline >= tomorrowStart && row.Deadline < upcomingHorizon ? 1 : 0),
                group.Sum(row => row.Open == 1 && row.Deadline < now ? 1 : 0),
                group.Sum(row => row.InProgress),
                group.Sum(row => row.Completed)))
            .SingleOrDefaultAsync(cancellationToken);

        return counts ?? new DashboardTaskCounts(0, 0, 0, 0, 0, 0, 0);
    }
}
