namespace TaskFlow.Api.Services.Assistant.HelpKnowledge;

/// <summary>A help topic as TFA may use it. <see cref="IsRestricted"/> means the user's role cannot do this, so
/// <see cref="Content"/> only says who can, and no steps are included.</summary>
public record HelpTopicDto(string Id, string Title, string Content, string? Link, bool IsRestricted);
