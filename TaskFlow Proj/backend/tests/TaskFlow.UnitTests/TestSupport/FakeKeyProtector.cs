using TaskFlow.Api.Integrations.Ai.KeyProtection;

namespace TaskFlow.UnitTests.TestSupport;

/// <summary>"Encrypts" a key by prefixing it, so a test can store a readable key or one that cannot be decrypted.</summary>
internal sealed class FakeKeyProtector : IAiKeyProtector
{
    public const string Prefix = "enc:";

    public static string Encrypted(string plainKey) => Prefix + plainKey;

    public string Protect(string plainKey) => Encrypted(plainKey);

    public string? Unprotect(string protectedKey) =>
        protectedKey.StartsWith(Prefix, StringComparison.Ordinal) ? protectedKey[Prefix.Length..] : null;
}
