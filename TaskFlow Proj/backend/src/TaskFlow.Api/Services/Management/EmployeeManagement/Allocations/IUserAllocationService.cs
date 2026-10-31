using TaskFlow.Api.Dtos.Management.ProjectManagement;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;

namespace TaskFlow.Api.Services.Management.EmployeeManagement.Allocations;

/// <summary>The single place that decides who may be on which project and department. Every write path goes through it, so
/// the rules (whole percentages 1 to 100, a total of at most 100 per user, one row per project) cannot be bypassed.</summary>
public interface IUserAllocationService
{
    /// <summary>The cap, in percent, on one user's allocations across all projects.</summary>
    const int MaxTotalPercent = 100;

    /// <summary>Checks a replacement set for one user (percentages, duplicate projects, positions and projects that exist,
    /// the 100% total) without changing anything. Throws <see cref="InvalidOperationException"/> naming the problem.</summary>
    System.Threading.Tasks.Task ValidateForUserAsync(IReadOnlyList<ProjectAllocationInput> inputs, CancellationToken cancellationToken);

    /// <summary>Checks department ids exist. Throws <see cref="InvalidOperationException"/> otherwise.</summary>
    System.Threading.Tasks.Task ValidateDepartmentsAsync(IReadOnlyList<int> departmentIds, CancellationToken cancellationToken);

    /// <summary>Stages a replacement of the user's project allocations and department memberships. A null list leaves that
    /// part unchanged; an empty list clears it. The caller saves, so it commits with the rest of the user change. Validate
    /// first.</summary>
    System.Threading.Tasks.Task StageForUserAsync(
        TaskFlow.Api.Models.Entities.User user,
        IReadOnlyList<ProjectAllocationInput>? allocations,
        IReadOnlyList<int>? departmentIds,
        CancellationToken cancellationToken);

    /// <summary>Replaces who is on one project. All-or-nothing: if any person would exceed 100% in total, nothing is saved
    /// and the message names that person with their total and what remains.</summary>
    System.Threading.Tasks.Task ReplaceProjectMembersAsync(
        int projectId,
        IReadOnlyList<ProjectMemberInput> members,
        CancellationToken cancellationToken);
}
