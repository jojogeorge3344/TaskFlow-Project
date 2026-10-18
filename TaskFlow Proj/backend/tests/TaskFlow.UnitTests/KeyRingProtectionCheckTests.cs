using System.Security.AccessControl;
using System.Security.Principal;
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

    [WindowsOnlyFact]
    public void AKeyFileThatCannotBeReadIsSkippedAndTheCheckDoesNotThrow()
    {
        WriteKey("key-a.xml", Unprotected);
        WriteKey("key-locked.xml", Unprotected);
        var locked = new FileInfo(Path.Combine(_folder, "key-locked.xml"));
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny);
        var security = locked.GetAccessControl();
        security.AddAccessRule(deny);
        locked.SetAccessControl(security);
        try
        {
            var count = KeyRingProtectionCheck.CountUnprotectedKeys(_folder);

            Assert.Equal(1, count);
        }
        finally
        {
            security.RemoveAccessRule(deny);
            locked.SetAccessControl(security);
        }
    }

    [WindowsOnlyFact]
    public void AFolderThatCannotBeListedGivesZeroInsteadOfThrowing()
    {
        WriteKey("key-a.xml", Unprotected);
        var folder = new DirectoryInfo(_folder);
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory, AccessControlType.Deny);
        var security = folder.GetAccessControl();
        security.AddAccessRule(deny);
        folder.SetAccessControl(security);
        try
        {
            var count = KeyRingProtectionCheck.CountUnprotectedKeys(_folder);

            Assert.Equal(0, count);
        }
        finally
        {
            security.RemoveAccessRule(deny);
            folder.SetAccessControl(security);
        }
    }

    [Fact]
    public void ACompleteCheckHasNoReasonForDoubt()
    {
        WriteKey("key-a.xml", Unprotected);
        WriteKey("key-b.xml", Protected);

        var result = KeyRingProtectionCheck.Check(_folder);

        Assert.Equal(1, result.UnprotectedKeys);
        Assert.Equal(0, result.SkippedFiles);
        Assert.False(result.FolderUnreadable);
        Assert.Null(result.CouldNotCheckReason);
    }

    [Fact]
    public void AFolderThatDoesNotExistYetIsNotAProblem()
    {
        var result = KeyRingProtectionCheck.Check(Path.Combine(_folder, "later"));

        Assert.Null(result.CouldNotCheckReason);
        Assert.Equal(0, result.UnprotectedKeys);
    }

    [WindowsOnlyFact]
    public void AnUnreadableKeyFileIsCountedAndExplained()
    {
        WriteKey("key-a.xml", Unprotected);
        WriteKey("key-locked.xml", Unprotected);
        var locked = new FileInfo(Path.Combine(_folder, "key-locked.xml"));
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny);
        var security = locked.GetAccessControl();
        security.AddAccessRule(deny);
        locked.SetAccessControl(security);
        try
        {
            var result = KeyRingProtectionCheck.Check(_folder);

            Assert.Equal(1, result.UnprotectedKeys);
            Assert.Equal(1, result.SkippedFiles);
            Assert.False(result.FolderUnreadable);
            Assert.Equal("1 key file(s) could not be read", result.CouldNotCheckReason);
        }
        finally
        {
            security.RemoveAccessRule(deny);
            locked.SetAccessControl(security);
        }
    }

    [WindowsOnlyFact]
    public void AFolderThatCannotBeListedIsExplained()
    {
        WriteKey("key-a.xml", Unprotected);
        var folder = new DirectoryInfo(_folder);
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory, AccessControlType.Deny);
        var security = folder.GetAccessControl();
        security.AddAccessRule(deny);
        folder.SetAccessControl(security);
        try
        {
            var result = KeyRingProtectionCheck.Check(_folder);

            Assert.True(result.FolderUnreadable);
            Assert.Equal("the folder could not be listed", result.CouldNotCheckReason);
        }
        finally
        {
            security.RemoveAccessRule(deny);
            folder.SetAccessControl(security);
        }
    }
}
