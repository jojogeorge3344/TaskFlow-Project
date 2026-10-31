namespace TaskFlow.Api.Services.Assistant.HelpKnowledge;

/// <summary>The written help for TaskFlow screens and workflows. TFA answers how-to questions only from here.</summary>
public interface IHelpKnowledgeBase
{
    /// <summary>The best matching topics for the question, for the caller's role. Empty when nothing matches.</summary>
    IReadOnlyList<HelpTopicDto> Search(string query, string role, int maxResults = 3);
}
