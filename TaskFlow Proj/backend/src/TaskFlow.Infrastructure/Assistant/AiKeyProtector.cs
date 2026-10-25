using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using TaskFlow.Application.Assistant;

namespace TaskFlow.Infrastructure.Assistant;

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
