namespace TaskFlow.Application.Users;

/// <summary>A self-service profile save. Role, department, username, employee code and account
/// status are deliberately absent from the contract, so they cannot be escalated by construction.</summary>
public record UpdateOwnProfileRequest(string? FullName, string? Email);

/// <summary>A self-service password change. The current password must be verified before the new
/// one is applied.</summary>
public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
