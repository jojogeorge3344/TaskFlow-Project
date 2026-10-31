using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Models.Entities;

/// <summary>A model an administrator can choose for an AI provider. The built-in ones are written in code and inserted
/// the first time the application runs on any machine; the administrator can add more, which are kept in the database.</summary>
public class AiModel
{
    public int Id { get; set; }
    public AiProviderKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>True for a model that comes from the code, false for one the administrator added.</summary>
    public bool IsBuiltIn { get; set; }
}
