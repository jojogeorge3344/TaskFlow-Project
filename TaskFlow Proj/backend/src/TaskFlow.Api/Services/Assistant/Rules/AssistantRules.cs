namespace TaskFlow.Api.Services.Assistant.Rules;

public static class AssistantRules
{
    public const int MaxMessageLength = 2000;
    public const int HistoryMessages = 12;
    public const int HistoryCharacters = 6000;
    public const int MaxToolRounds = 4;

    /// <summary>How many of the latest messages the panel loads. Older ones stay stored but are not sent to the browser.</summary>
    public const int ConversationMessages = 100;
}