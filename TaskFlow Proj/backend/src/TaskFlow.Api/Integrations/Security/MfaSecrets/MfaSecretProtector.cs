using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using TaskFlow.Api.Integrations.Security.OneTimeCodes;

namespace TaskFlow.Api.Integrations.Security.MfaSecrets;

/// <summary>Protects MFA keys and sign-in challenges with the persisted Data Protection key ring, so a copied
/// database alone cannot be used to generate codes.</summary>
public class MfaSecretProtector : IMfaSecretProtector
{
    public const string SecretPurpose = "TaskFlow.MfaSecrets";
    public const string ChallengePurpose = "TaskFlow.MfaChallenge";

    private readonly IDataProtector _secretProtector;
    private readonly IDataProtector _challengeProtector;
    private readonly TimeProvider _timeProvider;

    public MfaSecretProtector(IDataProtectionProvider provider, TimeProvider timeProvider)
    {
        _secretProtector = provider.CreateProtector(SecretPurpose);
        _challengeProtector = provider.CreateProtector(ChallengePurpose);
        _timeProvider = timeProvider;
    }

    public string Protect(byte[] secret) => _secretProtector.Protect(Base32.Encode(secret));

    public byte[]? Unprotect(string? protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
        {
            return null;
        }

        try
        {
            return Base32.Decode(_secretProtector.Unprotect(protectedSecret));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public string CreateChallenge(int userId, string securityStamp, TimeSpan lifetime)
    {
        var expires = _timeProvider.GetUtcNow().Add(lifetime).ToUnixTimeSeconds();
        return _challengeProtector.Protect(string.Create(CultureInfo.InvariantCulture, $"{userId}|{expires}|{securityStamp}"));
    }

    public (int UserId, string SecurityStamp)? ReadChallenge(string? challenge)
    {
        if (string.IsNullOrWhiteSpace(challenge))
        {
            return null;
        }

        try
        {
            var parts = _challengeProtector.Unprotect(challenge).Split('|', 3);
            if (parts.Length != 3
                || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
                || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
                || expires <= _timeProvider.GetUtcNow().ToUnixTimeSeconds())
            {
                return null;
            }

            return (userId, parts[2]);
        }
        catch (CryptographicException)
        {
            // Forged, tampered with, or from another key ring.
            return null;
        }
    }
}
