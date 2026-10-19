using Microsoft.Data.Sqlite;

namespace TaskFlow.Infrastructure.Persistence;

public static class DatabasePathResolver
{
    private const string SolutionFileName = "TaskFlow.sln";

    /// <summary>Resolves a relative data source against <paramref name="storageRoot"/>, or against the solution folder
    /// when no root is given (design-time tooling).</summary>
    public static string Resolve(string? connectionString, string? storageRoot = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString ?? string.Empty;
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (string.IsNullOrWhiteSpace(builder.DataSource) || Path.IsPathRooted(builder.DataSource))
        {
            return connectionString;
        }

        builder.DataSource = Path.GetFullPath(Path.Combine(storageRoot ?? FindSolutionDirectory(), builder.DataSource));

        return builder.ToString();
    }

    public static string FindSolutionDirectory()
    {
        var candidates = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };

        foreach (var candidate in candidates)
        {
            var directory = new DirectoryInfo(candidate);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
