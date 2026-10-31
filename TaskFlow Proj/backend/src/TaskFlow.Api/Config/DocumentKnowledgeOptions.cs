using Microsoft.Extensions.Options;
using TaskFlow.Api.Dtos.Settings.TfaSettings.Validators;

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

    /// <summary>The Ollama embedding model. Changing it re-embeds every document.</summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>The Ollama server for embeddings. Empty means the saved Ollama provider address, then the default.</summary>
    public string? OllamaBaseUrl { get; set; }

    /// <summary>The most chunks passed to the model for one message.</summary>
    public int TopK { get; set; } = 4;

    /// <summary>The lowest cosine similarity a chunk needs to be passed to the model.</summary>
    public double MinScore { get; set; } = 0.6;

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

        if (string.IsNullOrWhiteSpace(EmbeddingModel) || EmbeddingModel.Length > 100)
        {
            problems.Add("DocumentKnowledge:EmbeddingModel is required and at most 100 characters.");
        }

        if (!string.IsNullOrWhiteSpace(OllamaBaseUrl)
            && !(Uri.TryCreate(OllamaBaseUrl.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                 && !UpdateAiProviderRequestValidator.IsMetadataHost(uri)))
        {
            problems.Add("DocumentKnowledge:OllamaBaseUrl must be an http or https URL, and not a link-local or cloud metadata address.");
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
