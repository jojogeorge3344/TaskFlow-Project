namespace TaskFlow.Api.Models.Entities;

/// <summary>A runtime-editable, non-secret application setting stored as a key/value pair
/// (TDD §7.2). Secrets such as the SMTP password never live here.</summary>
public class ApplicationSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int? UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
