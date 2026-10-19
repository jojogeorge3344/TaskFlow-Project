namespace TaskFlow.Infrastructure.Persistence;

/// <summary>The <c>Storage</c> configuration section: the one folder that holds the database, the AI key ring and mail
/// attachments on this machine.</summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>C:\TaskFlow on Windows; a TaskFlow folder under the shared application data folder elsewhere.</summary>
    public static readonly string DefaultRoot = OperatingSystem.IsWindows()
        ? @"C:\TaskFlow"
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TaskFlow");

    public string Root { get; set; } = DefaultRoot;
}

/// <summary>Raised when the data folder cannot be created or written, so startup stops with a message that names it.</summary>
public class StorageUnavailableException : Exception
{
    public StorageUnavailableException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public static class StorageRoot
{
    /// <summary>The configured root as a full path, or <see cref="StorageOptions.DefaultRoot"/> when none is set.</summary>
    public static string Resolve(string? configuredRoot) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot) ? StorageOptions.DefaultRoot : configuredRoot);

    /// <summary>Creates the folder if needed and proves it is writable. Nothing is created anywhere else on failure.</summary>
    public static void EnsureAvailable(string root)
    {
        try
        {
            Directory.CreateDirectory(root);

            var probe = Path.Combine(root, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new StorageUnavailableException(
                $"TaskFlow cannot use its data folder '{root}': {exception.Message} "
                + "Create the folder and allow this account to write to it, or set Storage:Root to another folder.",
                exception);
        }
    }

    /// <summary>The database an earlier version kept in the solution's <c>database</c> folder, when it still exists and the
    /// data folder has no database of its own yet. That is an upgraded install that would otherwise start empty. Call it
    /// before the migration creates the new database.</summary>
    public static string? FindLegacyDatabase(string root, string solutionDirectory)
    {
        var legacy = Path.Combine(solutionDirectory, "database", "taskflow.db");

        return File.Exists(legacy) && !File.Exists(Path.Combine(root, "taskflow.db")) ? legacy : null;
    }

    /// <summary>Resolves a configured path: absolute paths are kept, relative ones sit under <paramref name="root"/>.</summary>
    public static string ResolveUnder(string root, string configured) =>
        Path.IsPathRooted(configured) ? configured : Path.GetFullPath(Path.Combine(root, configured));
}
