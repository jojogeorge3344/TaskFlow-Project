namespace TaskFlow.Api.Models.Entities;

/// <summary>A single-use permission to set a new password, issued after the user proved who they are with an
/// authenticator code. Only a hash of the token is stored.</summary>
public class PasswordResetGrant
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set when the token was spent, or when another reset made it pointless.</summary>
    public DateTime? UsedAt { get; set; }

    public User? User { get; set; }
}
