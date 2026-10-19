using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.UnitTests;

public class StorageRootTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), $"taskflow-storage-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    [Fact]
    public void Default_root_is_c_taskflow_on_windows_and_the_shared_app_data_folder_elsewhere()
    {
        var expected = OperatingSystem.IsWindows()
            ? @"C:\TaskFlow"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TaskFlow");

        Assert.Equal(expected, StorageOptions.DefaultRoot);
        Assert.Equal(expected, new StorageOptions().Root);
        Assert.Equal(Path.GetFullPath(expected), StorageRoot.Resolve(null));
        Assert.Equal(Path.GetFullPath(expected), StorageRoot.Resolve("  "));
        Assert.Equal(Path.GetFullPath(expected), StorageRoot.Resolve(string.Empty));
    }

    [Fact]
    public void Override_replaces_the_default_for_database_key_ring_and_attachments()
    {
        var root = StorageRoot.Resolve(_temp);

        var database = DatabasePathResolver.Resolve("Data Source=taskflow.db", root);

        Assert.Equal(root, Path.GetDirectoryName(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(database).DataSource));
        Assert.Equal(Path.Combine(root, "dp-keys"), StorageRoot.ResolveUnder(root, "dp-keys"));
        Assert.Equal(Path.Combine(root, "mail-attachments"), StorageRoot.ResolveUnder(root, "mail-attachments"));
        Assert.DoesNotContain("C:\\TaskFlow", database, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_legacy_database_is_reported_only_while_the_new_root_has_none()
    {
        var solution = Path.Combine(_temp, "solution");
        var root = Path.Combine(_temp, "root");
        Directory.CreateDirectory(Path.Combine(solution, "database"));
        Directory.CreateDirectory(root);

        Assert.Null(StorageRoot.FindLegacyDatabase(root, solution));

        var legacy = Path.Combine(solution, "database", "taskflow.db");
        File.WriteAllText(legacy, string.Empty);
        Assert.Equal(legacy, StorageRoot.FindLegacyDatabase(root, solution));

        File.WriteAllText(Path.Combine(root, "taskflow.db"), string.Empty);
        Assert.Null(StorageRoot.FindLegacyDatabase(root, solution));
    }

    [Fact]
    public void Absolute_paths_are_kept()
    {
        var absolute = Path.Combine(_temp, "elsewhere");

        Assert.Equal(absolute, StorageRoot.ResolveUnder(Path.Combine(_temp, "root"), absolute));
    }

    [Fact]
    public void EnsureAvailable_creates_a_missing_folder_and_leaves_no_probe_behind()
    {
        var root = Path.Combine(_temp, "new-root");

        StorageRoot.EnsureAvailable(root);

        Assert.True(Directory.Exists(root));
        Assert.Empty(Directory.GetFileSystemEntries(root));
    }

    [Fact]
    public void EnsureAvailable_fails_naming_the_folder_when_it_cannot_be_created()
    {
        Directory.CreateDirectory(_temp);
        var blocker = Path.Combine(_temp, "a-file");
        File.WriteAllText(blocker, "not a folder");
        var root = Path.Combine(blocker, "TaskFlow");

        var failure = Assert.Throws<StorageUnavailableException>(() => StorageRoot.EnsureAvailable(root));

        Assert.Contains(root, failure.Message);
        Assert.NotNull(failure.InnerException);
        Assert.False(Directory.Exists(root));
    }
}
