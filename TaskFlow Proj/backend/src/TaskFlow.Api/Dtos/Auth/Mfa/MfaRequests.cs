namespace TaskFlow.Api.Dtos.Auth.Mfa;

/// <summary>Start of setup: the current password proves it is really the account owner.</summary>
public record MfaSetupRequest(string? CurrentPassword);

/// <summary>Setup response: the key to type in and the URI behind the QR code. Shown once, never again.</summary>
public record MfaSetupResponse(string Key, string ProvisioningUri);

/// <summary>Confirms setup with the first code from the app.</summary>
public record MfaEnableRequest(string? Code);

/// <summary>Disabling MFA or regenerating backup codes needs both factors.</summary>
public record MfaPasswordCodeRequest(string? CurrentPassword, string? Code);

/// <summary>Backup codes in their displayed form. Returned once, when they are created.</summary>
public record MfaBackupCodesResponse(IReadOnlyList<string> Codes);

/// <summary>The second sign-in step: the challenge from the password step plus an authenticator or backup code.</summary>
public record MfaVerifyRequest(string? ChallengeToken, string? Code);

/// <summary>What <c>auth/login</c> returns instead of a session when the account has MFA on.</summary>
public record MfaChallengeResponse(bool MfaRequired, string ChallengeToken);

