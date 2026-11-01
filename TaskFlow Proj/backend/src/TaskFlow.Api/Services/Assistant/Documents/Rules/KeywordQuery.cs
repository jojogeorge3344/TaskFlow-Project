using System.Text;

namespace TaskFlow.Api.Services.Assistant.Documents.Rules;

/// <summary>A message turned into an FTS5 keyword query (design H2 of tfa-hybrid-document-search).
/// <see cref="FtsQuery"/> is null when no significant term is left.</summary>
public sealed record KeywordQuery(string? FtsQuery, bool HasExactIdentifier, IReadOnlyList<string> Terms)
{
    public const int MinTermLength = 2;

    /// <summary>Common English words that say nothing about which document is meant. Compared case-insensitively.</summary>
    public static readonly IReadOnlySet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "a", "about", "after", "all", "also", "am", "an", "and", "any", "are", "as", "at", "be", "been", "before", "being",
        "but", "by", "can", "could", "did", "do", "does", "doing", "done", "each", "explain", "for", "from", "get", "give",
        "had", "has", "have", "he", "her", "here", "him", "his", "how", "if", "in", "into", "is", "it", "its", "just",
        "know", "let", "like", "may", "me", "mean", "means", "might", "more", "most", "must", "my", "need", "no", "not",
        "of", "on", "or", "our", "please", "said", "say", "says", "shall", "she", "should", "show", "so", "some", "tell",
        "than", "that", "the", "their", "them", "then", "there", "these", "they", "this", "those", "to", "us", "used",
        "want", "was", "we", "were", "what", "when", "where", "which", "while", "who", "whom", "whose", "why", "will",
        "with", "would", "you", "your"
    };

    private static readonly char[] EdgePunctuation = ['-', '_', '.'];

    /// <summary>The significant terms of <paramref name="message"/>, each quoted so FTS5 reads it as a plain word (an
    /// operator such as AND, OR, NOT, NEAR, *, ^ or a column filter cannot be formed), joined by spaces, which FTS5
    /// reads as AND.</summary>
    public static KeywordQuery Build(string? message)
    {
        var terms = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in Split(message ?? string.Empty))
        {
            var term = raw.Trim(EdgePunctuation);
            if (term.Length < MinTermLength || StopWords.Contains(term) || !seen.Add(term))
            {
                continue;
            }

            terms.Add(term);
        }

        if (terms.Count == 0)
        {
            return new KeywordQuery(null, false, []);
        }

        var query = string.Join(' ', terms.Select(term => $"\"{term.Replace("\"", "\"\"")}\""));
        return new KeywordQuery(query, terms.Any(IsExactIdentifier), terms);
    }

    /// <summary>A term written like a name or ID rather than an ordinary word: it has a digit, a hyphen, an underscore, a
    /// dot, or a capital letter after its first character (NFR-14, TimeoutSeconds, gemini-3.8-flash, FRD).</summary>
    public static bool IsExactIdentifier(string term) =>
        term.Any(character => char.IsDigit(character) || character is '-' or '_' or '.')
        || term.Skip(1).Any(char.IsUpper);

    /// <summary>Splits on every character that is not a letter, a digit, '-', '_' or '.'.</summary>
    private static IEnumerable<string> Split(string message)
    {
        var current = new StringBuilder();
        foreach (var character in message)
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            {
                current.Append(character);
            }
            else if (current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
