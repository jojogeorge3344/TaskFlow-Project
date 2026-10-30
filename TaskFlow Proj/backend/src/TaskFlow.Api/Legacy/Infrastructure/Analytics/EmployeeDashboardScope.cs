using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Domain.Rules;
using TaskFlow.Infrastructure.Persistence;
using TaskEntity = TaskFlow.Domain.Entities.Task;

namespace TaskFlow.Infrastructure.Analytics;

public class EmployeeDashboardScope : IEmployeeDashboardScope
{
    private readonly AppDbContext _dbContext;

    public EmployeeDashboardScope(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<TaskEntity> OwnTasks(IQueryable<TaskEntity> tasks, int userId) =>
        tasks.Where(TaskBucketRules.IsCounted()).Where(task =>
            task.AssignedToUserId == userId
            || _dbContext.TaskRecipients.Any(recipient => recipient.TaskId == task.Id && recipient.UserId == userId));
}
