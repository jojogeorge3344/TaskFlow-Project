using System.Globalization;

namespace TaskFlow.Api.Common.Utils;

/// <summary>How typed values are written to the string-valued application settings table.</summary>
public static class SettingValues
{
    public static string Format(bool value) => value ? "true" : "false";

    public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
