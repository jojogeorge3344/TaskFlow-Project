namespace TaskFlow.Infrastructure.Assistant;

/// <summary>Finds keys in the Data Protection key ring that are stored unprotected. Turning on key protection (DPAPI)
/// only affects keys created afterwards, so a key ring made before that keeps a plain master key until it is deleted
/// and the API keys are entered again. This lets the application say so at startup.</summary>
public static class KeyRingProtectionCheck
{
    /// <summary>The number of key files in the folder that hold a plain <c>masterKey</c> element. Zero when the folder
    /// does not exist yet, every key is protected, or it cannot be checked. It is advisory and best-effort: it never
    /// throws, so it can never stop the application from starting, and it only counts: key material is never returned.</summary>
    public static int CountUnprotectedKeys(string keyRingFolder)
    {
        var count = 0;
        try
        {
            if (!Directory.Exists(keyRingFolder))
            {
                return 0;
            }

            foreach (var file in Directory.EnumerateFiles(keyRingFolder, "key-*.xml"))
            {
                if (HasPlainMasterKey(file))
                {
                    count++;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // The folder cannot be listed (permissions, a drive that went away). The keys counted so far are reported.
        }

        return count;
    }

    private static bool HasPlainMasterKey(string file)
    {
        try
        {
            return File.ReadAllText(file).Contains("<masterKey", StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // A file that cannot be read right now is skipped; the check runs again at the next start.
            return false;
        }
    }
}
