namespace TaskFlow.UnitTests;

/// <summary>A test that needs Windows (file permissions, DPAPI). On other systems it is reported as skipped, not as
/// passed, so a green run never claims a check that did not run.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Needs Windows.";
        }
    }
}
