namespace TaskFlow.Api.Integrations.Ai.KeyProtection;

/// <summary>Encrypts AI provider API keys for storage. Keys are decrypted only in memory, inside a provider call.</summary>
public interface IAiKeyProtector
{
    string Protect(string plainKey);

    /// <summary>The key, or null when the stored value cannot be read (for example the key ring was replaced).</summary>
    string? Unprotect(string protectedKey);
}
