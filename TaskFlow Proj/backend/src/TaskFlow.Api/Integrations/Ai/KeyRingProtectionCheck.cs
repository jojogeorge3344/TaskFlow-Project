namespace TaskFlow.Api.Integrations.Ai;

/// <summary>What a key ring check found. <see cref="CouldNotCheckReason"/> is set when part of the ring could not be
/// inspected, so an unprotected key may have gone unnoticed.</summary>
public sealed record KeyRingCheckResult(int UnprotectedKeys, int SkippedFiles, bool FolderUnreadable)
{
    public string? CouldNotCheckReason =>
        FolderUnreadable
            ? "the folder could not be listed"
            : SkippedFiles > 0
                ? $"{SkippedFiles} key file(s) could not be read"
                : null;
}

/// <summary>Finds keys in the Data Protection key ring that are stored unprotected. Turning on key protection (DPAPI)
/// only affects keys created afterwards, so a key ring made before that keeps a plain master key until it is deleted
/// and the API keys are entered again. This lets the application say so at startup.</summary>
public static class KeyRingProtectionCheck
{
    /// <summary>The number of key files in the folder that hold a plain <c>masterKey</c> element, as a convenience for
    /// <see cref="Check"/>.</summary>
    public static int CountUnprotectedKeys(string keyRingFolder) => Check(keyRingFolder).UnprotectedKeys;

    /// <summary>Looks at the key files and counts those that hold a plain <c>masterKey</c> element. It is advisory and
    /// best-effort: it never throws, so it can never stop the application from starting, and it says what it could not
    /// inspect. It only counts: key material is never returned. A folder that does not exist yet has nothing to report.</summary>
    public static KeyRingCheckResult Check(string keyRingFolder)
    {
        var unprotected = 0;
        var skipped = 0;
        try
        {
            if (!Directory.Exists(keyRingFolder))
            {
                return new KeyRingCheckResult(0, 0, false);
            }

            foreach (var file in Directory.EnumerateFiles(keyRingFolder, "key-*.xml"))
            {
                switch (Inspect(file))
                {
                    case FileState.PlainKey:
                        unprotected++;
                        break;
                    case FileState.Unreadable:
                        skipped++;
                        break;
                }
            }
        }
        catch (Exception exception) when (IsAccessProblem(exception))
        {
            // The folder cannot be listed (permissions, a drive that went away). What was counted so far is kept.
            return new KeyRingCheckResult(unprotected, skipped, true);
        }

        return new KeyRingCheckResult(unprotected, skipped, false);
    }

    private enum FileState
    {
        Protected,
        PlainKey,
        Unreadable
    }

    private static FileState Inspect(string file)
    {
        try
        {
            return File.ReadAllText(file).Contains("<masterKey", StringComparison.Ordinal) ? FileState.PlainKey : FileState.Protected;
        }
        catch (Exception exception) when (IsAccessProblem(exception))
        {
            // A file that cannot be read right now is skipped and counted; the check runs again at the next start.
            return FileState.Unreadable;
        }
    }

    private static bool IsAccessProblem(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or System.Security.SecurityException;
}
