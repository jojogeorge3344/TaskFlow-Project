namespace TaskFlow.Application.Auth;

/// <summary>An Administrator generates a new password for a user using a one-time reset token.
/// The password itself is generated server-side and never supplied or returned.</summary>
public record ResetPasswordRequest(string Token);
