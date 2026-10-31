using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Models.Entities;

/// <summary>Something TFA has prepared for the user but not done. Only the user's own confirm request runs it.</summary>
public class AssistantPendingAction
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ConversationId { get; set; }
    public AssistantActionKind Kind { get; set; }

    /// <summary>The exact details shown to the user, as JSON. This is what is executed on confirm.</summary>
    public string PayloadJson { get; set; } = "{}";

    public AssistantActionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
