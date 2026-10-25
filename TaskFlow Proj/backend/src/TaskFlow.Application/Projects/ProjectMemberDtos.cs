using TaskFlow.Application.Users;

namespace TaskFlow.Application.Projects;

public record PositionDto(int Id, string Name);

public record CreatePositionRequest(string Name);

/// <summary>A person on a project's team, with the position they hold there and their percentage.</summary>
public record ProjectMemberDto(int UserId, string FullName, string Email, int PositionId, string PositionName, int Percent, bool IsManager, bool IsManagerRole);

/// <summary>Replaces the whole team of a project. An empty list removes everyone.</summary>
public record ReplaceProjectMembersRequest(IReadOnlyList<ProjectMemberInput>? Members);
