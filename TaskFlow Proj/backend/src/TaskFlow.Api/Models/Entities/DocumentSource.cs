namespace TaskFlow.Api.Models.Entities;

/// <summary>One indexed Markdown file of the documents folder. Rebuildable from the folder at any time.</summary>
public class DocumentSource
{
    public int Id { get; set; }

    /// <summary>The path under the documents folder, with '/' separators.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>The first level-one heading, or the file name.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>SHA-256 of the file's bytes as lowercase hex; an unchanged hash means the file is not embedded again.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>The embedding model the chunks were made with. A different configured model re-embeds the file.</summary>
    public string EmbeddingModel { get; set; } = string.Empty;

    public int ChunkCount { get; set; }

    public DateTime IndexedAt { get; set; }

    public List<DocumentChunk> Chunks { get; set; } = [];
}
