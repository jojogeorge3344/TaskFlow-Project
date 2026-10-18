namespace TaskFlow.Infrastructure.Assistant;

/// <summary>Finds keys in the Data Protection key ring that are stored unprotected. Turning on key protection (DPAPI)
/// only affects keys created afterwards, so a key ring made before that keeps a plain master key until it is deleted
/// and the API keys are entered again. This lets the application say so at startup.</summary>
public static class KeyRingProtectionCheck
{
    /// <summary>The number of key files in the folder that hold a plain <c>masterKey</c> element. Zero when the folder
    /// does not exist yet or every key is protected. Only counts: it never reads key material into the result.</summary>
    public static int CountUnprotectedKeys(string keyRingFolder)
    {
        if (!Directory.Exists(keyRingFolder))
        {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(keyRingFolder, "key-*.xml"))
        {
            try
            {
                if (File.ReadAllText(file).Contains("<masterKey", StringComparison.Ordinal))
                {
                    count++;
                }
            }
            catch (IOException)
            {
                // A file that cannot be read right now is skipped; the check runs again at the next start.
            }
        }

        return count;
    }
}
