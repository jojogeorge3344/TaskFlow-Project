using System.Text.RegularExpressions;

namespace TaskFlow.Application.Assistant;

/// <summary>A safety net that spots passwords and API keys in text. TFA never accepts them in chat: a message that
/// looks like it contains one is not sent to any provider and is not stored. It is a screen, not a guarantee, which is
/// why the tools also never take a password argument.</summary>
public static partial class SecretScreen
{
    public const string RedactedText = "[hidden]";

    // "my password is Hunter2!", "password: abc123", "change my password to NewPass#9". The value must contain a digit
    // or a symbol, so ordinary sentences such as "the password is incorrect" are not caught.
    [GeneratedRegex(
        @"\b(?:password|passwd|pwd|passcode)\b[^.\n]{0,20}?(?:\bis\b|\bwas\b|=|:|\bto\b)\s*[""']?(?<value>\S{6,})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PasswordPhrase();

    [GeneratedRegex(@"\bsk-[A-Za-z0-9_\-]{16,}", RegexOptions.CultureInvariant)]
    private static partial Regex OpenAiKey();

    [GeneratedRegex(@"\bAIza[0-9A-Za-z_\-]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex GoogleKey();

    [GeneratedRegex(@"\bBearer\s+[A-Za-z0-9._\-]{20,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerToken();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]*", RegexOptions.CultureInvariant)]
    private static partial Regex Jwt();

    // A long unbroken run of letters and digits that mixes both is almost certainly a token, not a word.
    [GeneratedRegex(@"\b(?=[A-Za-z0-9_\-]*[A-Za-z])(?=[A-Za-z0-9_\-]*\d)[A-Za-z0-9_\-]{32,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex LongToken();

    public static bool ContainsSecret(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return HasPasswordValue(text)
            || OpenAiKey().IsMatch(text)
            || GoogleKey().IsMatch(text)
            || BearerToken().IsMatch(text)
            || Jwt().IsMatch(text)
            || LongToken().IsMatch(text);
    }

    /// <summary>The text with anything that looks like a key or password value replaced, for tool results that might
    /// otherwise echo a secret back to the model.</summary>
    public static string Redact(string text)
    {
        var result = PasswordPhrase().Replace(text, match =>
            IsPasswordLike(match.Groups["value"].Value)
                ? match.Value.Replace(match.Groups["value"].Value, RedactedText, StringComparison.Ordinal)
                : match.Value);

        result = OpenAiKey().Replace(result, RedactedText);
        result = GoogleKey().Replace(result, RedactedText);
        result = BearerToken().Replace(result, "Bearer " + RedactedText);
        result = Jwt().Replace(result, RedactedText);
        return LongToken().Replace(result, RedactedText);
    }

    private static bool HasPasswordValue(string text)
    {
        foreach (Match match in PasswordPhrase().Matches(text))
        {
            if (IsPasswordLike(match.Groups["value"].Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPasswordLike(string value) =>
        value.Any(char.IsDigit) || value.Any(character => !char.IsLetterOrDigit(character) && character != '.' && character != ',');
}
