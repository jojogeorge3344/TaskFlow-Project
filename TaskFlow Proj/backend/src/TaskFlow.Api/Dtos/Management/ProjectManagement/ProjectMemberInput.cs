namespace TaskFlow.Api.Dtos.Management.ProjectManagement;

/// <summary>One row of a project's team.</summary>
public record ProjectMemberInput(int UserId, int PositionId, int Percent, bool IsManager = false);
