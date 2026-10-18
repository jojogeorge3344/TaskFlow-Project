using TaskFlow.Infrastructure.Assistant;

namespace TaskFlow.UnitTests;

public class KeyRingProtectionCheckTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"taskflow-keyring-check-{Guid.NewGuid():N}");

    public KeyRingProtectionCheckTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private void WriteKey(string name, string body) => File.WriteAllText(Path.Combine(_folder, name), body);

    private const string Unprotected =
        "<key id=\"a\"><descriptor><descriptor><encryption algorithm=\"AES_256_CBC\" /><masterKey p4:requiresEncryption=\"true\"><value>AAAA</value></masterKey></descriptor></descriptor></key>";

    private const string Protected =
        "<key id=\"b\"><descriptor><descriptor><encryption algorithm=\"AES_256_CBC\" /><encryptedKey><!-- encrypted with DPAPI --><value>BBBB</value></encryptedKey></descriptor></descriptor></key>";

    [Fact]
    public void AMissingFolderCountsAsNothingToWarnAbout()
    {
        Assert.Equal(0, KeyRingProtectionCheck.CountUnprotectedKeys(Path.Combine(_folder, "does-not-exist")));
    }

    [Fact]
    public void AnEmptyFolderHasNoUnprotectedKeys()
    {
        Assert.Equal(0, KeyRingProtectionCheck.CountUnprotectedKeys(_folder));
    }

    [Fact]
    public void ProtectedKeysAreNotCounted()
    {
        WriteKey("key-b.xml", Protected);

        Assert.Equal(0, KeyRingProtectionCheck.CountUnprotectedKeys(_folder));
    }

    [Fact]
    public void AKeyWithAPlainMasterKeyIsCounted()
    {
        WriteKey("key-a.xml", Unprotected);

        Assert.Equal(1, KeyRingProtectionCheck.CountUnprotectedKeys(_folder));
    }

    [Fact]
    public void OnlyUnprotectedKeysAreCountedWhenTheRingIsMixed()
    {
        WriteKey("key-a.xml", Unprotected);
        WriteKey("key-b.xml", Protected);
        WriteKey("key-c.xml", Unprotected);

        Assert.Equal(2, KeyRingProtectionCheck.CountUnprotectedKeys(_folder));
    }

    [Fact]
    public void FilesThatAreNotKeysAreIgnored()
    {
        WriteKey("notes.xml", Unprotected);
        WriteKey("revocation-1.xml", Unprotected);

        Assert.Equal(0, KeyRingProtectionCheck.CountUnprotectedKeys(_folder));
    }
}
