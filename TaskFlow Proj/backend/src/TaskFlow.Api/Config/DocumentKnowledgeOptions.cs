using Microsoft.Extensions.Options;

namespace TaskFlow.Api.Config;

/// <summary>Stops the API at startup when the <c>DocumentKnowledge</c> section has an unusable value, naming the key.</summary>
public class DocumentKnowledgeOptionsValidation : IValidateOptions<DocumentKnowledgeOptions>
{
    public ValidateOptionsResult Validate(string? name, DocumentKnowledgeOptions options) =>
        options.Validate() is { Count: > 0 } problems ? ValidateOptionsResult.Fail(problems) : ValidateOptionsResult.Success;
}

/// <summary>The <c>DocumentKnowledge</c> configuration section: the documents folder TFA answers from, and how it is
/// chunked, embedded and searched.</summary>
public class DocumentKnowledgeOptions
{
    public const string SectionName = "DocumentKnowledge";

    /// <summary>Turns indexing and retrieval on or off (tests and rollbacks switch it off).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The folder of Markdown documents. A relative path resolves against the API's content root
    /// (<c>backend/src/TaskFlow.Api</c>), so the default is the workspace's <c>TF - Documents</c> folder.</summary>
    public string SourcePath { get; set; } = "../../../../TF - Documents";

    /// <summary>The Google Gemini embedding model. Changing it re-embeds every document.</summary>
    public string EmbeddingModel { get; set; } = "gemini-embedding-2";

    /// <summary>The length of each embedding vector Gemini returns (<c>outputDimensionality</c>). Changing it re-embeds
    /// every document.</summary>
    public int EmbeddingDimensions { get; set; } = 768;

    /// <summary>The model and vector size as stored with each document, so a change to either re-embeds everything.</summary>
    public string IndexModel => $"{EmbeddingModel.Trim()}@{EmbeddingDimensions}";

    /// <summary>The most chunks passed to the model for one message.</summary>
    public int TopK { get; set; } = 4;

    /// <summary>The lowest cosine similarity a chunk needs to be passed to the model. 0.65 was measured with gemini-embedding-2
    /// at 768 dimensions: covered questions scored 0.73 to 0.84 against the right section, unrelated ones at most 0.56.</summary>
    public double MinScore { get; set; } = 0.65;

    /// <summary>The longest chunk, in characters, before a section is split.</summary>
    public int MaxChunkCharacters { get; set; } = 1500;

    /// <summary>The most document text, in characters, passed to the model for one message.</summary>
    public int MaxContextCharacters { get; set; } = 4000;

    /// <summary>Minutes between checks of the documents folder.</summary>
    public int RescanMinutes { get; set; } = 5;

    /// <summary>Sections with these headings are not indexed. Headings set in configuration are added to these defaults
    /// (the configuration binder appends to an array), so this is not listed in appsettings.json.</summary>
    public string[] ExcludedHeadings { get; set; } = ["Document Control", "Revision History", "Approvals"];

    /// <summary>The documents folder as a full path: an absolute <see cref="SourcePath"/> is kept, a relative one resolves
    /// against <paramref name="contentRoot"/>.</summary>
    public string ResolveSourcePath(string contentRoot) =>
        Path.GetFullPath(Path.IsPathRooted(SourcePath) ? SourcePath : Path.Combine(contentRoot, SourcePath));

    /// <summary>The problems with these values, empty when they are usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(SourcePath))
        {
            problems.Add("DocumentKnowledge:SourcePath is required.");
        }

        if (string.IsNullOrWhiteSpace(EmbeddingModel) || EmbeddingModel.Trim().Length > 90)
        {
            problems.Add("DocumentKnowledge:EmbeddingModel is required and at most 90 characters.");
        }

        if (EmbeddingDimensions is < 128 or > 3072)
        {
            problems.Add("DocumentKnowledge:EmbeddingDimensions must be between 128 and 3072.");
        }

        if (TopK is < 1 or > 20)
        {
            problems.Add("DocumentKnowledge:TopK must be between 1 and 20.");
        }

        if (MinScore is < 0 or > 1)
        {
            problems.Add("DocumentKnowledge:MinScore must be between 0 and 1.");
        }

        if (MaxChunkCharacters is < 300 or > 6000)
        {
            problems.Add("DocumentKnowledge:MaxChunkCharacters must be between 300 and 6000.");
        }

        if (MaxContextCharacters < MaxChunkCharacters)
        {
            problems.Add("DocumentKnowledge:MaxContextCharacters must be at least MaxChunkCharacters.");
        }

        if (RescanMinutes < 1)
        {
            problems.Add("DocumentKnowledge:RescanMinutes must be at least 1.");
        }

        return problems;
    }
}
