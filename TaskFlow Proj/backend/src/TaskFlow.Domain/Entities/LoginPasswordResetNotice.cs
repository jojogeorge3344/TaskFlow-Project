namespace TaskFlow.Domain.Entities;

/// <summary>The Login Screen credential notice for one account. One row per user (unique index),
/// always bound to the latest completed reset of either origin, so an earlier event can never
/// reappear and no notice can cross accounts. It ends at the first successful sign-in afterwards or when the
/// user updates the password from the Login Screen; it has no time limit.</summary>
public class LoginPasswordResetNotice
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public int PasswordResetRequestId { get; set; }

    public PasswordResetRequest? PasswordResetRequest { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Set when the user signed in successfully with the newly issued credentials.</summary>
    public DateTime? ClearedAt { get; set; }
}
