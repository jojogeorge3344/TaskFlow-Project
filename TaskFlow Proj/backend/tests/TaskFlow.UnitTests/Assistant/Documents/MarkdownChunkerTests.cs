using TaskFlow.Api.Services.Assistant.Documents.Rules;

namespace TaskFlow.UnitTests.Assistant.Documents;

public class MarkdownChunkerTests
{
    private static readonly string[] Excluded = ["Document Control", "Revision History", "Approvals"];

    private static readonly string Long = string.Join(' ', Enumerable.Repeat("This sentence makes the section long enough to stand alone.", 5));

    private static ChunkedDocument Chunk(string markdown, int max = 1500, string fileName = "doc.md") =>
        MarkdownChunker.Chunk(markdown, fileName, max, Excluded);

    [Fact]
    public void HeadingPathsFollowTheHeadingsBelowTheTitle()
    {
        var document = Chunk($"""
            # The Title

            ## 4. Requirements
            {Long}

            ### 4.1 Module
            {Long}

            #### 4.1.2 Screen: Create Task
            {Long}

            ## 5. Workflows
            {Long}
            """);

        Assert.Equal("The Title", document.Title);
        Assert.Equal(
            ["4. Requirements", "4. Requirements › 4.1 Module", "4. Requirements › 4.1 Module › 4.1.2 Screen: Create Task", "5. Workflows"],
            document.Chunks.Select(chunk => chunk.HeadingPath));
        Assert.Equal([0, 1, 2, 3], document.Chunks.Select(chunk => chunk.Ordinal));
    }

    [Fact]
    public void TheEmbeddedTextCarriesTheTitleAndSection()
    {
        var chunk = Assert.Single(Chunk($"# Guide\n\n## Login\n{Long}").Chunks);

        Assert.Equal(Long, chunk.Text);
        Assert.Equal($"Document: Guide\nSection: Login\n\n{Long}", chunk.EmbeddingText);
    }

    [Fact]
    public void HeadingsInsideFencedCodeAreIgnored()
    {
        var document = Chunk($"""
            # Title

            ## Real
            {Long}

            ```bash
            # not a heading
            ## also not a heading
            ```
            """);

        var chunk = Assert.Single(document.Chunks);
        Assert.Equal("Real", chunk.HeadingPath);
        Assert.Contains("# not a heading", chunk.Text);
    }

    [Fact]
    public void AShortSectionMergesIntoTheNextSibling()
    {
        var document = Chunk($"""
            # Title

            ## Parent

            ### Short
            Only a line.

            ### Next
            {Long}
            """);

        var chunk = Assert.Single(document.Chunks);
        Assert.Equal("Parent › Next", chunk.HeadingPath);
        Assert.StartsWith("Short\nOnly a line.\n\n", chunk.Text);
        Assert.EndsWith(Long, chunk.Text);
    }

    [Fact]
    public void AShortSectionIsKeptWhenNothingFollowsUnderTheSameParent()
    {
        var document = Chunk($"""
            # Title

            ## First
            ### Short
            Only a line.

            ## Second
            {Long}
            """);

        Assert.Equal(["First › Short", "Second"], document.Chunks.Select(chunk => chunk.HeadingPath));
        Assert.Equal("Only a line.", document.Chunks[0].Text);
    }

    [Fact]
    public void ALongTableSplitsBetweenRowsWithTheHeaderRepeated()
    {
        var rows = Enumerable.Range(1, 40).Select(index => $"| FR-{index:00} | The system shall do requirement number {index} properly. |");
        var document = Chunk($"# T\n\n## Rules\n| ID | Requirement |\n|---|---|\n{string.Join('\n', rows)}", max: 500);

        Assert.True(document.Chunks.Count > 1);
        Assert.All(document.Chunks, chunk => Assert.StartsWith("| ID | Requirement |\n|---|---|\n| FR-", chunk.Text));
        var allRows = document.Chunks.SelectMany(chunk => chunk.Text.Split('\n').Skip(2)).ToList();
        Assert.Equal(rows, allRows);
    }

    [Fact]
    public void AnOverLongParagraphSplitsAtSentenceEnds()
    {
        var sentences = Enumerable.Range(1, 30).Select(index => $"Sentence number {index} explains one more rule.").ToList();
        var document = Chunk($"# T\n\n## Rules\n{string.Join(' ', sentences)}", max: 300);

        Assert.True(document.Chunks.Count > 1);
        Assert.All(document.Chunks, chunk => Assert.EndsWith(".", chunk.Text));
        Assert.Equal(sentences, document.Chunks.SelectMany(chunk => chunk.Text.Split(". ")).Select(s => s.EndsWith('.') ? s : s + "."));
    }

    [Fact]
    public void ExcludedHeadingsAreSkippedWithTheirSubsections()
    {
        var document = Chunk($"""
            # Title

            ## Document Control
            | Version | Date |
            |---|---|
            | 1.0 | 2026-10-31 |

            ### Revision History
            {Long}

            ## 1. Introduction
            {Long}

            ### Approvals / Sign-off
            {Long}

            ## 12. Approvals
            {Long}
            """);

        Assert.Equal(["1. Introduction"], document.Chunks.Select(chunk => chunk.HeadingPath));
    }

    [Fact]
    public void TheTitleFallsBackToTheFileName()
    {
        var document = Chunk($"## Only a section\n{Long}", fileName: "08-create-task-screen.md");

        Assert.Equal("08-create-task-screen", document.Title);
        Assert.Equal("Only a section", Assert.Single(document.Chunks).HeadingPath);
    }

    [Fact]
    public void TextBeforeAnySectionIsFiledUnderTheTitle()
    {
        var document = Chunk($"# Title\n{Long}\n\n## Next\n{Long}");

        Assert.Equal(["Title", "Next"], document.Chunks.Select(chunk => chunk.HeadingPath));
    }

    [Fact]
    public void AnEmptyDocumentHasNoChunks()
    {
        Assert.Empty(Chunk("# Title\n\n## Empty\n\n").Chunks);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(800)]
    [InlineData(1500)]
    public void NoChunkExceedsTheLimitPlusARepeatedTableHeader(int max)
    {
        var document = MarkdownChunker.Chunk(TrimmedFrd(), "FRD-Task-Management.md", max, Excluded);

        Assert.All(document.Chunks, chunk =>
        {
            var lines = chunk.Text.Split('\n');
            var header = lines.Length > 2 && lines[0].StartsWith('|') && lines[1].StartsWith("|-") ? lines[0].Length + lines[1].Length + 2 : 0;
            Assert.True(chunk.Text.Length <= max + header, $"{chunk.Text.Length} > {max} + {header}: {chunk.HeadingPath}");
        });
    }

    [Fact]
    public void TheTrimmedTaskManagementFrdChunksIntoItsSections()
    {
        var document = MarkdownChunker.Chunk(TrimmedFrd(), "FRD-Task-Management.md", 1500, Excluded);

        Assert.Equal("Functional Requirements Document – Task Management", document.Title);
        Assert.Contains(document.Chunks, chunk =>
            chunk.HeadingPath == "4. Functional Requirements by Module › 4.1 Module: Task Management (TASK) › 4.1.2 Screen: Create Task (and Copy Task)"
            && chunk.Text.Contains("FR-TASK-19"));
        Assert.Contains(document.Chunks, chunk => chunk.HeadingPath.StartsWith("1. Introduction"));
        Assert.DoesNotContain(document.Chunks, chunk => chunk.HeadingPath.Contains("Document Control")
                                                        || chunk.HeadingPath.Contains("Revision History")
                                                        || chunk.HeadingPath.Contains("Approvals"));
        var requirementPieces = document.Chunks.Where(chunk => chunk.Text.Contains("| FR-TASK-")).ToList();
        Assert.True(requirementPieces.Count > 1, "The requirements table should be split into several chunks.");
        Assert.All(requirementPieces, chunk =>
        {
            var header = chunk.Text.IndexOf("| ID | Requirement | Priority |", StringComparison.Ordinal);
            Assert.True(header >= 0 && header < chunk.Text.IndexOf("| FR-TASK-", StringComparison.Ordinal), "Each piece repeats the table header.");
        });
    }

    private static string TrimmedFrd() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assistant", "Documents", "Fixtures", "FRD-Task-Management-trimmed.md"));
}
