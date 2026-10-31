using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Services.Monitoring.TaskManagement.Access;

namespace TaskFlow.Api.Authorization;

public class TaskAccessRequirement : IAuthorizationRequirement
{
}

/// <summary>Resource-level access rule for task-scoped endpoints. The assignee, the task
/// creator, any additional recipient, or a user whose role holds tasks.view.all may access a task.</summary>
public class TaskAccessHandler : AuthorizationHandler<TaskAccessRequirement>
{
    private readonly ITaskAccessService _access;

    public TaskAccessHandler(ITaskAccessService access)
    {
        _access = access;
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

        int? userId = int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : null;
        var canViewAll = context.User.HasClaim(AuthClaims.Permission, PermissionKeys.TasksViewAll);

        if (await _access.CanAccessAsync(taskId, userId, canViewAll))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}
