namespace TaskFlow.Api.Services.Assistant.Documents;

public enum DocumentContextKind
{
    /// <summary>Chunks matched and are passed to the model.</summary>
    Found,

    /// <summary>The search ran and nothing reached the minimum score.</summary>
    NoMatch,

    /// <summary>The documents could not be searched.</summary>
    Unavailable
}

public enum DocumentUnavailableReason
{
    /// <summary>The first index has not stored anything yet.</summary>
    Preparing,

    /// <summary>The embedding server or model cannot be used, the index is empty, or indexing is switched off.</summary>
    CannotSearch
}

/// <summary>The outcome of a document search for one message.</summary>
public record DocumentContext(DocumentContextKind Kind, IReadOnlyList<ScoredChunk> Chunks, DocumentUnavailableReason? Reason = null)
{
    public static DocumentContext Found(IReadOnlyList<ScoredChunk> chunks) => new(DocumentContextKind.Found, chunks);

    public static DocumentContext NoMatch { get; } = new(DocumentContextKind.NoMatch, []);

    public static DocumentContext Unavailable(DocumentUnavailableReason reason) => new(DocumentContextKind.Unavailable, [], reason);
}
