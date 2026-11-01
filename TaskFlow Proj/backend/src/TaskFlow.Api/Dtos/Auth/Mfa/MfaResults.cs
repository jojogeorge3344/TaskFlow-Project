namespace TaskFlow.Api.Dtos.Auth.Mfa;

public enum MfaVerifyOutcome
{
    Success,
    Invalid,
    /// <summary>Too many recent wrong codes; even a correct code is refused until the lock ends.</summary>
    LockedOut
}

/// <summary>What setup returns once: the key to type in and the URI behind the QR code.</summary>
public record MfaSetupResult(string Key, string ProvisioningUri);
