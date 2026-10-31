using System.Text;
using System.Text.RegularExpressions;

namespace TaskFlow.Api.Services.Assistant.Documents.Rules;

/// <summary>A piece of a document ready to embed. <see cref="Text"/> is what the model is shown; <see cref="EmbeddingText"/>
/// adds the document title and heading path so the vector keeps the chunk's context.</summary>
public record MarkdownChunk(int Ordinal, string HeadingPath, string Text, string EmbeddingText);

public record ChunkedDocument(string Title, IReadOnlyList<MarkdownChunk> Chunks);

/// <summary>Splits a Markdown document into chunks that follow its headings (design D5). Pure: no I/O.</summary>
public static partial class MarkdownChunker
{
    public const string HeadingSeparator = " › ";

    /// <summary>A section with less text than this is merged into the next section under the same parent.</summary>
    public const int MinSectionCharacters = 200;

    public const int MaxHeadingPathLength = 1000;

    private sealed record Section(int Level, string Heading, string[] Path, string Body)
    {
        public string HeadingPath => string.Join(HeadingSeparator, Path);

        public string[] ParentPath => Path.Length == 0 ? [] : Path[..^1];
    }

    public static ChunkedDocument Chunk(string markdown, string fileName, int maxChunkCharacters, IReadOnlyCollection<string> excludedHeadings)
    {
        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var title = FindTitle(lines) ?? Path.GetFileNameWithoutExtension(fileName);
        var sections = Merge(ReadSections(lines, excludedHeadings));

        var chunks = new List<MarkdownChunk>();
        foreach (var section in sections)
        {
            var headingPath = Truncate(section.HeadingPath.Length == 0 ? title : section.HeadingPath);
            foreach (var piece in Split(section.Body, maxChunkCharacters))
            {
                chunks.Add(new MarkdownChunk(chunks.Count, headingPath, piece, $"Document: {title}\nSection: {headingPath}\n\n{piece}"));
            }
        }

        return new ChunkedDocument(title, chunks);
    }

    // ------------------------------------------------------------------------------------------- headings

    [GeneratedRegex(@"^ {0,3}(#{1,6})[ \t]+(.+?)[ \t]*#*[ \t]*$")]
    private static partial Regex HeadingLine();

    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})")]
    private static partial Regex FenceLine();

    [GeneratedRegex(@"^\d+(\.\d+)*\.?\s+")]
    private static partial Regex SectionNumber();

    private static string? FindTitle(string[] lines)
    {
        string? fence = null;
        foreach (var line in lines)
        {
            if (ToggleFence(line, ref fence) || fence is not null)
            {
                continue;
            }

            if (HeadingLine().Match(line) is { Success: true } match && match.Groups[1].Length == 1)
            {
                return match.Groups[2].Value.Trim();
            }
        }

        return null;
    }

    /// <summary>Tracks fenced code blocks: returns true for a fence line and updates <paramref name="fence"/>.</summary>
    private static bool ToggleFence(string line, ref string? fence)
    {
        if (FenceLine().Match(line) is not { Success: true } match)
        {
            return false;
        }

        var marker = match.Groups[1].Value;
        if (fence is null)
        {
            fence = marker;
        }
        else if (marker[0] == fence[0] && marker.Length >= fence.Length)
        {
            fence = null;
        }

        return true;
    }

    private static bool IsExcluded(string heading, IReadOnlyCollection<string> excludedHeadings)
    {
        var name = SectionNumber().Replace(heading, string.Empty).Trim();
        return excludedHeadings.Any(excluded =>
            name.Equals(excluded, StringComparison.OrdinalIgnoreCase)
            || (name.StartsWith(excluded, StringComparison.OrdinalIgnoreCase) && name.Length > excluded.Length && !char.IsLetterOrDigit(name[excluded.Length])));
    }

    /// <summary>Every section in order. The level-one heading is the document title, so it is not part of a heading path.
    /// An excluded section is dropped with everything below it.</summary>
    private static List<Section> ReadSections(string[] lines, IReadOnlyCollection<string> excludedHeadings)
    {
        var sections = new List<Section>();
        var stack = new string?[7];
        var current = new Section(0, string.Empty, [], string.Empty);
        var body = new StringBuilder();
        int? excludedLevel = null;
        string? fence = null;

        void Close()
        {
            if (excludedLevel is null)
            {
                sections.Add(current with { Body = body.ToString().Trim('\n', ' ') });
            }

            body.Clear();
        }

        foreach (var line in lines)
        {
            var isFence = ToggleFence(line, ref fence);
            if (!isFence && fence is null && HeadingLine().Match(line) is { Success: true } match)
            {
                Close();

                var level = match.Groups[1].Length;
                var heading = match.Groups[2].Value.Trim();
                stack[level] = heading;
                Array.Clear(stack, level + 1, stack.Length - level - 1);

                if (excludedLevel is { } excluded && level <= excluded)
                {
                    excludedLevel = null;
                }

                if (excludedLevel is null && IsExcluded(heading, excludedHeadings))
                {
                    excludedLevel = level;
                }

                current = new Section(level, heading, stack.Skip(2).Take(level - 1).OfType<string>().ToArray(), string.Empty);
                continue;
            }

            body.Append(line).Append('\n');
        }

        Close();
        return sections;
    }

    /// <summary>Drops sections with no text and folds a short section into the next one under the same parent, keeping
    /// its heading as a line so its words still count.</summary>
    private static List<Section> Merge(List<Section> sections)
    {
        var merged = new List<Section>();
        Section? pending = null;

        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];
            if (section.Body.Length == 0)
            {
                continue;
            }

            var carried = string.Empty;
            if (pending is not null)
            {
                if (IsUnder(section.Path, pending.ParentPath))
                {
                    carried = pending.Body + "\n\n";
                }
                else
                {
                    merged.Add(pending);
                }

                pending = null;
            }

            var next = sections.Skip(index + 1).FirstOrDefault(candidate => candidate.Body.Length > 0);
            if (carried.Length + section.Body.Length < MinSectionCharacters && next is not null && IsUnder(next.Path, section.ParentPath))
            {
                pending = section with { Body = carried + (section.Heading.Length == 0 ? section.Body : $"{section.Heading}\n{section.Body}") };
                continue;
            }

            merged.Add(section with { Body = carried + section.Body });
        }

        if (pending is not null)
        {
            merged.Add(pending);
        }

        return merged;
    }

    private static bool IsUnder(string[] path, string[] parent) =>
        path.Length > parent.Length && path.Take(parent.Length).SequenceEqual(parent);

    private static string Truncate(string headingPath) =>
        headingPath.Length <= MaxHeadingPathLength ? headingPath : headingPath[..(MaxHeadingPathLength - 1)] + "…";

    // ------------------------------------------------------------------------------------------- splitting

    private enum BlockKind
    {
        Text,
        Table,
        Code
    }

    private sealed record Block(BlockKind Kind, List<string> Lines)
    {
        public string Text => string.Join('\n', Lines);
    }

    /// <summary>The section text in pieces no longer than <paramref name="max"/>, except that a piece of a long table also
    /// carries the table's header rows.</summary>
    private static List<string> Split(string text, int max)
    {
        if (text.Length <= max)
        {
            return [text];
        }

        var pieces = new List<string>();
        var current = new StringBuilder();

        void Add(string part)
        {
            if (current.Length > 0 && current.Length + 2 + part.Length > max)
            {
                pieces.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append("\n\n");
            }

            current.Append(part);
        }

        foreach (var block in Blocks(text))
        {
            var blockText = block.Text;
            if (blockText.Length <= max)
            {
                Add(blockText);
                continue;
            }

            foreach (var part in block.Kind switch
                     {
                         BlockKind.Table => SplitTable(block.Lines, max),
                         BlockKind.Code => SplitLines(block.Lines, max),
                         _ => SplitSentences(blockText, max)
                     })
            {
                // A piece of a long block always starts a chunk of its own.
                if (current.Length > 0)
                {
                    pieces.Add(current.ToString());
                    current.Clear();
                }

                current.Append(part);
            }
        }

        if (current.Length > 0)
        {
            pieces.Add(current.ToString());
        }

        return pieces;
    }

    private static List<Block> Blocks(string text)
    {
        var blocks = new List<Block>();
        Block? block = null;
        string? fence = null;

        foreach (var line in text.Split('\n'))
        {
            var wasInFence = fence is not null;
            var isFence = ToggleFence(line, ref fence);

            if (isFence || wasInFence)
            {
                if (block?.Kind != BlockKind.Code || (isFence && !wasInFence))
                {
                    blocks.Add(block = new Block(BlockKind.Code, []));
                }

                block.Lines.Add(line);
                if (wasInFence && fence is null)
                {
                    block = null;
                }

                continue;
            }

            if (line.Trim().Length == 0)
            {
                block = null;
                continue;
            }

            var kind = line.TrimStart().StartsWith('|') ? BlockKind.Table : BlockKind.Text;
            if (block?.Kind != kind)
            {
                blocks.Add(block = new Block(kind, []));
            }

            block.Lines.Add(line);
        }

        return blocks;
    }

    [GeneratedRegex(@"^\s*\|?\s*:?-{3,}")]
    private static partial Regex TableSeparator();

    /// <summary>Splits a table between rows. Each piece repeats the header and separator rows.</summary>
    private static IEnumerable<string> SplitTable(List<string> lines, int max)
    {
        var headerRows = lines.Count > 1 && TableSeparator().IsMatch(lines[1]) ? 2 : 0;
        var header = string.Join('\n', lines.Take(headerRows));
        var rows = new StringBuilder();

        foreach (var row in lines.Skip(headerRows))
        {
            foreach (var part in row.Length > max ? SplitSentences(row, max) : [row])
            {
                if (rows.Length > 0 && rows.Length + 1 + part.Length > max)
                {
                    yield return WithHeader(header, rows.ToString());
                    rows.Clear();
                }

                if (rows.Length > 0)
                {
                    rows.Append('\n');
                }

                rows.Append(part);
            }
        }

        if (rows.Length > 0)
        {
            yield return WithHeader(header, rows.ToString());
        }
    }

    private static string WithHeader(string header, string rows) => header.Length == 0 ? rows : $"{header}\n{rows}";

    private static IEnumerable<string> SplitLines(List<string> lines, int max)
    {
        var current = new StringBuilder();
        foreach (var line in lines)
        {
            foreach (var part in line.Length > max ? HardSplit(line, max) : [line])
            {
                if (current.Length > 0 && current.Length + 1 + part.Length > max)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append('\n');
                }

                current.Append(part);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceEnd();

    /// <summary>Splits a long paragraph at sentence ends; a sentence longer than the limit is cut at a space.</summary>
    private static IEnumerable<string> SplitSentences(string text, int max)
    {
        var current = new StringBuilder();
        foreach (var sentence in SentenceEnd().Split(text).Where(sentence => sentence.Length > 0))
        {
            foreach (var part in sentence.Length > max ? HardSplit(sentence, max) : [sentence])
            {
                if (current.Length > 0 && current.Length + 1 + part.Length > max)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append(' ');
                }

                current.Append(part);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static IEnumerable<string> HardSplit(string text, int max)
    {
        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(max, text.Length - start);
            if (start + length < text.Length)
            {
                var space = text.LastIndexOf(' ', start + length - 1, length);
                if (space > start)
                {
                    length = space - start;
                }
            }

            yield return text.Substring(start, length).Trim();
            start += length;
        }
    }
}
