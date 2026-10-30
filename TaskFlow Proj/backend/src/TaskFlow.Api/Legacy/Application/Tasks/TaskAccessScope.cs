namespace TaskFlow.Application.Tasks;

/// <summary>Resource-level access rule for a task: assignee, creator, an additional recipient,
/// or a caller whose role holds the tasks.view.all permission. Mirrors the API's TaskAccessHandler (defense in depth).</summary>
public static class TaskAccessScope
{
    public static bool CanAccess(
        int userId,
        bool canViewAll,
        TaskFlow.Api.Models.Entities.Task task,
        IReadOnlyCollection<int> recipientUserIds)
    {
        if (canViewAll)
        {
            return true;
        }

        if (task.AssignedToUserId == userId || task.CreatedByUserId == userId)
        {
            return true;
        }

        return recipientUserIds.Contains(userId);
    }
}
