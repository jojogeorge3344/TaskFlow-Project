using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace TaskFlow.Api.Integrations.Ai;

public class AiKeyProtector : IAiKeyProtector
{
    public const string Purpose = "TaskFlow.AiProviderKeys";

    private readonly IDataProtector _protector;

    public AiKeyProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plainKey) => _protector.Protect(plainKey);

    public string? Unprotect(string protectedKey)
    {
        try
        {
            return _protector.Unprotect(protectedKey);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
