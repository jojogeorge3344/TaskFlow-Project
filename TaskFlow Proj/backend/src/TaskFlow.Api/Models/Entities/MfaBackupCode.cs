namespace TaskFlow.Api.Models.Entities;

/// <summary>A single-use recovery code for a user with MFA on. Only a hash is stored.</summary>
public class MfaBackupCode
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public User? User { get; set; }
}
