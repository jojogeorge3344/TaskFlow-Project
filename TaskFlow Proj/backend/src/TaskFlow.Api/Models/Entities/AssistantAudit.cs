using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Models.Entities;

/// <summary>A record that an action was prepared, confirmed, cancelled, expired or refused. It holds no message
/// bodies, passwords or keys.</summary>
public class AssistantAudit
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AssistantActionKind Kind { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public DateTime At { get; set; }
}
