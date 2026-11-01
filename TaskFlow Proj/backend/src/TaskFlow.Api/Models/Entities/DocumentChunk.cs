namespace TaskFlow.Api.Models.Entities;

/// <summary>One section (or part of a section) of an indexed document, with its embedding.</summary>
public class DocumentChunk
{
    public int Id { get; set; }
    public int DocumentSourceId { get; set; }
    public DocumentSource? DocumentSource { get; set; }

    /// <summary>The chunk's position in its document, from 0.</summary>
    public int Ordinal { get; set; }

    /// <summary>The headings above the chunk joined with " › ", outermost first.</summary>
    public string HeadingPath { get; set; } = string.Empty;

    /// <summary>The section text, as given to the model.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The L2-normalised embedding as little-endian float32 values.</summary>
    public byte[] Embedding { get; set; } = [];

    public int Dimensions { get; set; }
}
