using FluentValidation;

namespace TaskFlow.Application.Mfa;

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

public class MfaSetupRequestValidator : AbstractValidator<MfaSetupRequest>
{
    public MfaSetupRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
    }
}

public class MfaEnableRequestValidator : AbstractValidator<MfaEnableRequest>
{
    public MfaEnableRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}

public class MfaPasswordCodeRequestValidator : AbstractValidator<MfaPasswordCodeRequest>
{
    public MfaPasswordCodeRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}

public class MfaVerifyRequestValidator : AbstractValidator<MfaVerifyRequest>
{
    public MfaVerifyRequestValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}

/// <summary>Password recovery from the login page with an authenticator code.</summary>
public record AuthenticatorRecoveryRequest(string? Identifier, string? Code);

public class AuthenticatorRecoveryRequestValidator : AbstractValidator<AuthenticatorRecoveryRequest>
{
    public AuthenticatorRecoveryRequestValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}
