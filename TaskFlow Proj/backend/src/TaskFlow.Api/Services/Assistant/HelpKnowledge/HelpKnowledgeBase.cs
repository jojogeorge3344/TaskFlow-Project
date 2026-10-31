using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Common.Exceptions;

namespace TaskFlow.Api.Services.Assistant.HelpKnowledge;

/// <summary>Help topics shipped as embedded markdown files (<c>Assistant/Knowledge/*.md</c>), each with a small header
/// giving its title, the roles it applies to, search keywords and an optional screen link. Matching is plain keyword
/// scoring: the topic set is small, so no embeddings are needed.</summary>
public class HelpKnowledgeBase : IHelpKnowledgeBase
{
    private const int MinimumScore = 3;

    private static readonly char[] Separators = [' ', '\t', '\n', '\r', ',', '.', '?', '!', ';', ':', '"', '(', ')', '/', '-'];

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "how", "can", "you", "what", "where", "when", "why", "who", "does", "did", "are", "was",
        "with", "from", "this", "that", "have", "has", "not", "but", "any", "all", "get", "use", "want", "need",
        "please", "tell", "show", "about", "into", "your", "mine", "our", "its", "his", "her", "too", "would", "could",
        "should", "will", "just", "like", "make", "able", "way", "ways", "let"
    };

    private readonly IReadOnlyList<Topic> _topics;

    public HelpKnowledgeBase()
        : this(LoadEmbedded())
    {
    }

    internal HelpKnowledgeBase(IReadOnlyList<Topic> topics)
    {
        _topics = topics;
    }

    public IReadOnlyList<HelpTopicDto> Search(string query, string role, int maxResults = 3)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalized = query.ToLowerInvariant();
        var tokens = Tokenize(normalized);

        return _topics
            .Select(topic => (Topic: topic, Score: Score(topic, normalized, tokens)))
            .Where(match => match.Score >= MinimumScore)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Topic.Id, StringComparer.Ordinal)
            .Take(maxResults)
            .Select(match => ToDto(match.Topic, role))
            .ToList();
    }

    private static HelpTopicDto ToDto(Topic topic, string role)
    {
        if (topic.AppliesTo(role))
        {
            return new HelpTopicDto(topic.Id, topic.Title, topic.Body, topic.Link, false);
        }

        var note = string.IsNullOrWhiteSpace(topic.OthersNote)
            ? $"{topic.Title} is not available to your role."
            : topic.OthersNote;
        return new HelpTopicDto(topic.Id, topic.Title, note, null, true);
    }

    private static int Score(Topic topic, string normalizedQuery, IReadOnlyList<string> tokens)
    {
        var score = 0;
        foreach (var keyword in topic.Keywords)
        {
            if (keyword.Contains(' '))
            {
                if (normalizedQuery.Contains(keyword, StringComparison.Ordinal))
                {
                    score += 6;
                }
            }
            else if (tokens.Any(token => SameWord(token, keyword)))
            {
                score += 4;
            }
        }

        foreach (var token in tokens)
        {
            if (topic.KeywordWords.Contains(token) || topic.KeywordWords.Contains(token.TrimEnd('s')))
            {
                score += 3;
            }

            if (topic.TitleWords.Contains(token))
            {
                score += 2;
            }
        }

        return score;
    }

    /// <summary>Whole-word match that also treats a plain plural as the same word ("notification" and "notifications").</summary>
    private static bool SameWord(string token, string keyword) =>
        token == keyword || token == keyword + "s" || token + "s" == keyword;

    private static List<string> Tokenize(string text) =>
        text.Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length >= 3 && !StopWords.Contains(word))
            .Distinct()
            .ToList();

    private static IReadOnlyList<Topic> LoadEmbedded()
    {
        var assembly = typeof(HelpKnowledgeBase).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Assistant.Knowledge.", StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal))
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                return Topic.Parse(IdFromResource(name), reader.ReadToEnd());
            })
            .OrderBy(topic => topic.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static string IdFromResource(string resourceName)
    {
        var withoutExtension = resourceName[..^".md".Length];
        return withoutExtension[(withoutExtension.LastIndexOf('.') + 1)..];
    }

    internal sealed class Topic
    {
        private readonly HashSet<string> _roles;

        private Topic(string id, string title, string[] roles, string? others, string[] keywords, string? link, string body)
        {
            Id = id;
            Title = title;
            _roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
            OthersNote = others;
            Keywords = keywords;
            Link = link;
            Body = body;
            KeywordWords = new HashSet<string>(keywords.SelectMany(k => k.Split(' ')).Where(w => w.Length >= 3), StringComparer.Ordinal);
            TitleWords = new HashSet<string>(Tokenize(title.ToLowerInvariant()), StringComparer.Ordinal);
        }

        public string Id { get; }
        public string Title { get; }
        public string? OthersNote { get; }
        public string[] Keywords { get; }
        public string? Link { get; }
        public string Body { get; }
        public HashSet<string> KeywordWords { get; }
        public HashSet<string> TitleWords { get; }

        public bool AppliesTo(string role) => _roles.Contains("all") || _roles.Contains(role);

        public static Topic Parse(string id, string text)
        {
            var normalized = text.Replace("\r\n", "\n");
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var body = normalized;

            if (normalized.StartsWith("---\n", StringComparison.Ordinal))
            {
                var end = normalized.IndexOf("\n---", 4, StringComparison.Ordinal);
                if (end > 0)
                {
                    foreach (var line in normalized[4..end].Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var colon = line.IndexOf(':');
                        if (colon > 0)
                        {
                            fields[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                        }
                    }

                    body = normalized[(end + 4)..].Trim();
                }
            }

            string[] Split(string? value) =>
                (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new Topic(
                id,
                fields.GetValueOrDefault("title") ?? id,
                Split(fields.GetValueOrDefault("roles") ?? "all"),
                fields.GetValueOrDefault("others"),
                Split(fields.GetValueOrDefault("keywords")).Select(k => k.ToLowerInvariant()).ToArray(),
                fields.GetValueOrDefault("link"),
                body);
        }
    }
}
