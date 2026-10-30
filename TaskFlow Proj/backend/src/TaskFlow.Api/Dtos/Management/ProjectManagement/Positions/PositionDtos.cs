namespace TaskFlow.Api.Dtos.Management.ProjectManagement.Positions;

public record PositionDto(int Id, string Name);

public record CreatePositionRequest(string Name);
