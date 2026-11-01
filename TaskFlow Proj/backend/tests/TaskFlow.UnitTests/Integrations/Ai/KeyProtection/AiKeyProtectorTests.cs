using Microsoft.AspNetCore.DataProtection;
using TaskFlow.Api.Integrations.Ai.KeyProtection;

namespace TaskFlow.UnitTests.Integrations.Ai.KeyProtection;

public class AiKeyProtectorTests
{
    private static AiKeyProtector Create(IDataProtectionProvider? provider = null) =>
        new(provider ?? new EphemeralDataProtectionProvider());

    [Fact]
    public void ProtectedValueRoundTrips()
    {
        var protector = Create();

        var stored = protector.Protect("sk-test-1234567890");

        Assert.Equal("sk-test-1234567890", protector.Unprotect(stored));
    }

    [Fact]
    public void StoredTextIsNotThePlainKey()
    {
        var stored = Create().Protect("sk-test-1234567890");

        Assert.DoesNotContain("sk-test", stored);
        Assert.DoesNotContain("1234567890", stored);
    }

    [Fact]
    public void ADifferentPurposeCannotReadTheValue()
    {
        var provider = new EphemeralDataProtectionProvider();
        var stored = Create(provider).Protect("secret-key");

        var otherPurpose = provider.CreateProtector("Some.Other.Purpose");

        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => otherPurpose.Unprotect(stored));
    }

    [Fact]
    public void AReplacedKeyRingYieldsNullInsteadOfThrowing()
    {
        var stored = Create(new EphemeralDataProtectionProvider()).Protect("secret-key");

        var unreadable = Create(new EphemeralDataProtectionProvider()).Unprotect(stored);

        Assert.Null(unreadable);
    }
}
