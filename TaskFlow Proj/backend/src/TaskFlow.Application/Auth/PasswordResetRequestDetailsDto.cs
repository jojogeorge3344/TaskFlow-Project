
namespace TaskFlow.Application.Auth;

/// <summary>One password-reset request a user made. Status is Pending, Used or Expired.</summary>
public record PasswordResetRequestEntryDto(int Id, DateTime RequestedAt, DateTime ExpiresAt, string Status);

/// <summary>What an Administrator sees after opening a reset link: the user's details and how many times
/// they have asked for a password reset, newest first.</summary>
public record PasswordResetRequestDetailsDto(
    TaskFlow.Application.Users.UserDto User,
    int RequestCount,
    DateTime? LastRequestedAt,
    DateTime LinkExpiresAt,
    IReadOnlyList<PasswordResetRequestEntryDto> Requests);
