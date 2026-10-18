using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TaskFlow.IntegrationTests;

/// <summary>Starts the real application against a key ring folder and checks what it says at startup. DPAPI is a
/// Windows feature, so the warning only exists there.</summary>
public class KeyRingStartupWarningTests : IClassFixture<TaskFlowApiFactory>, IDisposable
{
    private const string PlainKey =
        "<key id=\"a\"><descriptor><descriptor><encryption algorithm=\"AES_256_CBC\" /><masterKey requiresEncryption=\"true\"><value>SECRETMATERIAL</value></masterKey></descriptor></descriptor></key>";

    private readonly TaskFlowApiFactory _factory;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"taskflow-keyring-startup-{Guid.NewGuid():N}");

    public KeyRingStartupWarningTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private WebApplicationFactory<Program> StartWith(CapturingLoggerProvider logs)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["AiProviders:KeyRingPath"] = _folder }));
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });

        // Creating a client starts the host, which runs the startup code.
        using var client = factory.CreateClient();
        return factory;
    }

    [WindowsOnlyFact]
    public void AKeyRingWithAPlainKeyMakesTheApiWarnAndSaysHowToFixIt()
    {
        File.WriteAllText(Path.Combine(_folder, "key-a.xml"), PlainKey);
        var logs = new CapturingLoggerProvider();

        using var factory = StartWith(logs);

        var warning = Assert.Single(logs.Lines, line => line.Contains("stored unprotected"));
        Assert.Contains("1 key(s)", warning);
        Assert.Contains(_folder, warning);
        Assert.Contains("delete that folder", warning);
        Assert.Contains("enter the AI provider API keys again", warning);
        Assert.DoesNotContain("SECRETMATERIAL", string.Join("\n", logs.Lines));
    }

    [WindowsOnlyFact]
    public void AnEmptyKeyRingGivesNoWarning()
    {
        var logs = new CapturingLoggerProvider();

        using var factory = StartWith(logs);

        Assert.DoesNotContain(logs.Lines, line => line.Contains("stored unprotected"));
    }

    [WindowsOnlyFact]
    public void AProtectedKeyGivesNoWarning()
    {
        File.WriteAllText(
            Path.Combine(_folder, "key-b.xml"),
            "<key id=\"b\"><descriptor><descriptor><encryption algorithm=\"AES_256_CBC\" /><encryptedKey><value>PROTECTED</value></encryptedKey></descriptor></descriptor></key>");
        var logs = new CapturingLoggerProvider();

        using var factory = StartWith(logs);

        Assert.DoesNotContain(logs.Lines, line => line.Contains("stored unprotected"));
    }

    [WindowsOnlyFact]
    public void AKeyFileTheApiCannotReadDoesNotStopItFromStarting()
    {
        var path = Path.Combine(_folder, "key-locked.xml");
        File.WriteAllText(path, PlainKey);
        var file = new FileInfo(path);
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny);
        var security = file.GetAccessControl();
        security.AddAccessRule(deny);
        file.SetAccessControl(security);
        var logs = new CapturingLoggerProvider();
        try
        {
            using var factory = StartWith(logs);

            Assert.DoesNotContain(logs.Lines, line => line.Contains("stored unprotected"));
        }
        finally
        {
            security.RemoveAccessRule(deny);
            file.SetAccessControl(security);
        }
    }
}
