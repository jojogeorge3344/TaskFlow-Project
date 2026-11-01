namespace TaskFlow.Api.Dtos.Auth.PasswordRecovery;

/// <summary>Password recovery from the login page with an authenticator code.</summary>
public record AuthenticatorRecoveryRequest(string? Identifier, string? Code);

/// <summary>Step 2 of recovery from the login page: the single-use token from the authenticator step plus the new password.</summary>
public record RecoverPasswordRequest(string? ResetToken, string? NewPassword, string? ConfirmPassword);

/// <summary>Returned after a correct authenticator code; the token is needed for the last step and works once.</summary>
public record RecoveryTokenResponse(string ResetToken);

public enum RecoveryVerifyStatus
{
    Verified,
    /// <summary>The account, the code or the account's state is wrong; never says which.</summary>
    Failed,
    /// <summary>The account or the caller's address has used up its attempts for now.</summary>
    Throttled
}

/// <summary>The outcome of the authenticator step, with the reset token when it was verified.</summary>
public record RecoveryVerifyResult(RecoveryVerifyStatus Status, string? ResetToken = null);
