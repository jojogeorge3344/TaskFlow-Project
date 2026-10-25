using System.Text.RegularExpressions;

namespace TaskFlow.Application.Users;

/// <summary>What a contact mobile number looks like: international E.164, a plus sign followed by 8 to 15 digits with no
/// leading zero. Numbers are contact details only; nothing is ever sent to them.</summary>
public static partial class MobileNumberPolicy
{
    public const string InvalidMessage =
        "Mobile number must be in international format, for example +919876543210.";

    [GeneratedRegex(@"^\+[1-9][0-9]{7,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164Pattern();

    /// <summary>Strips the spaces, dashes, dots and brackets people type around a number. Returns null for blank input.</summary>
    public static string? Normalize(string? mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber))
        {
            return null;
        }

        var cleaned = new string(mobileNumber.Where(c => !(char.IsWhiteSpace(c) || c is '-' or '.' or '(' or ')')).ToArray());
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>True when the input, once normalised, is a valid E.164 number.</summary>
    public static bool IsValid(string? mobileNumber)
    {
        var normalized = Normalize(mobileNumber);
        return normalized is not null && E164Pattern().IsMatch(normalized);
    }
}
