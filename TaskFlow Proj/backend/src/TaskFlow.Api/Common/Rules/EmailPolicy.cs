using System.Text.RegularExpressions;

namespace TaskFlow.Api.Common.Rules;

/// <summary>The one place that knows the fixed employee email domain and what a valid local part looks like.</summary>
public static partial class EmailPolicy
{
    public const string Domain = "taskflow.com";

    public const string Suffix = "@" + Domain;

    public const int MaxLocalPartLength = 64;

    public const string InvalidMessage =
        "Email must be a name using letters, digits, dot, underscore or hyphen followed by " + Suffix + ".";

    [GeneratedRegex("^[a-z0-9_-]+(\\.[a-z0-9_-]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex LocalPartPattern();

    /// <summary>Trims and lowercases an address, or returns an empty string for null.</summary>
    public static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValidLocalPart(string? localPart) =>
        !string.IsNullOrEmpty(localPart)
        && localPart.Length <= MaxLocalPartLength
        && LocalPartPattern().IsMatch(localPart);

    /// <summary>True when the address is a valid local part on the fixed domain.</summary>
    public static bool IsValid(string? email)
    {
        var normalized = Normalize(email);
        if (!normalized.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return false;
        }

        return IsValidLocalPart(normalized[..^Suffix.Length]);
    }

    /// <summary>The part before the domain of an address that passed <see cref="IsValid"/>.</summary>
    public static string LocalPart(string normalizedEmail) => normalizedEmail[..^Suffix.Length];
}
