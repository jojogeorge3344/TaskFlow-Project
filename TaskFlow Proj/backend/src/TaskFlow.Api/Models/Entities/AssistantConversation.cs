namespace TaskFlow.Api.Models.Entities;

/// <summary>One TFA chat belonging to one user. The user's latest conversation is the active one.</summary>
public class AssistantConversation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<AssistantMessage> Messages { get; set; } = new List<AssistantMessage>();
}
