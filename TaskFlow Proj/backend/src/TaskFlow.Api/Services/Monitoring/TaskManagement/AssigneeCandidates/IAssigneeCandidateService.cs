using TaskFlow.Api.Dtos.Monitoring.TaskManagement;

namespace TaskFlow.Api.Services.Monitoring.TaskManagement.AssigneeCandidates;

/// <summary>Who on a project's team should take a new task (Screen 8).</summary>
public interface IAssigneeCandidateService
{
    /// <summary>The project's assignable team in pick order (the first is the default assignee), an empty list when the
    /// project has no team, or null when the project does not exist or is deleted.</summary>
    Task<IReadOnlyList<AssigneeCandidateDto>?> GetCandidatesAsync(int projectId, CancellationToken cancellationToken);
}
