namespace TaskFlow.Api.Dtos.Profile;

/// <summary>A self-service profile save. Role, project, username, employee code and account
/// status are deliberately absent from the contract, so they cannot be escalated by construction.</summary>
public record UpdateOwnProfileRequest(string? FullName, string? Email, string? MobileNumber = null);

/// <summary>A self-service password change. The authenticator code is the proof of identity; the
/// current password is not asked for.</summary>
public record ChangePasswordRequest(
    string? NewPassword,
    string? MfaCode = null);

