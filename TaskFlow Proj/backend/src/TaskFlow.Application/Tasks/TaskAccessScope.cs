using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tasks;

/// <summary>Resource-level access rule for a task: assignee, creator, an additional recipient,
/// or a Manager/Administrator. Mirrors the API's TaskAccessHandler (defense in depth).</summary>
public static class TaskAccessScope
{
    public static bool CanAccess(
        int userId,
        string currentUserRole,
        TaskFlow.Domain.Entities.Task task,
        IReadOnlyCollection<int> recipientUserIds)
    {
        if (currentUserRole is nameof(Role.Manager) or nameof(Role.Administrator))
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