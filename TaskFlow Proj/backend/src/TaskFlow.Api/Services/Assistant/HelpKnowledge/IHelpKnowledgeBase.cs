namespace TaskFlow.Api.Services.Assistant.HelpKnowledge;

/// <summary>A help topic as TFA may use it. <see cref="IsRestricted"/> means the user's role cannot do this, so
/// <see cref="Content"/> only says who can, and no steps are included.</summary>
public record HelpTopicDto(string Id, string Title, string Content, string? Link, bool IsRestricted);

/// <summary>The written help for TaskFlow screens and workflows. TFA answers how-to questions only from here.</summary>
public interface IHelpKnowledgeBase
{
    /// <summary>The best matching topics for the question, for the caller's role. Empty when nothing matches.</summary>
    IReadOnlyList<HelpTopicDto> Search(string query, string role, int maxResults = 3);
}
