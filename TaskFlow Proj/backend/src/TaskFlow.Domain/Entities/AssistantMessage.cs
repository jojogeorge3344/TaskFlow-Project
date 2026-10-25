using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

/// <summary>A message in provider-neutral form, so a conversation survives a switch of AI provider.</summary>
public class AssistantMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public AssistantConversation? Conversation { get; set; }
    public AssistantMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    /// <summary>What the panel needs to render beyond the text, as JSON: the kind of message, links, a ranked task
    /// list, or the pending action the message offers.</summary>
    public string? MetaJson { get; set; }

    /// <summary>Which provider produced an assistant message.</summary>
    public AiProviderKind? ProviderKind { get; set; }

    public DateTime CreatedAt { get; set; }
}
