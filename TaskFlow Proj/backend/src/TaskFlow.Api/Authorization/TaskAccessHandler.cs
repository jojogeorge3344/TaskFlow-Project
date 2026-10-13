using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Api.Authorization;

public class TaskAccessRequirement : IAuthorizationRequirement
{
}

/// <summary>Resource-level access rule for task-scoped endpoints. The assignee, the task
/// creator, any additional recipient, or a Manager/Administrator may access a task.</summary>
public class TaskAccessHandler : AuthorizationHandler<TaskAccessRequirement>
{
    private readonly AppDbContext _dbContext;

    public TaskAccessHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TaskAccessRequirement requirement)
    {
        if (context.Resource is not int taskId)
        {
            context.Fail();
            return;
        }

        var task = await _dbContext.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == taskId && !candidate.IsDeleted);

        if (task is null)
        {
            context.Fail();
            return;
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role);
        if (role is nameof(Role.Administrator) or nameof(Role.Manager))
        {
            context.Succeed(requirement);
            return;
        }

        if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.Fail();
            return;
        }

        var isRelated = task.AssignedToUserId == userId
            || task.CreatedByUserId == userId
            || await _dbContext.TaskRecipients.AnyAsync(recipient =>
                recipient.TaskId == taskId && recipient.UserId == userId);

        if (isRelated)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}