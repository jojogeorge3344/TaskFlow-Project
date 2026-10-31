using TaskFlow.Api.Models.Entities;
using Task = System.Threading.Tasks.Task;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Assistant.Documents;

/// <summary>The stored TFA document index: one source per Markdown file and its embedded chunks.</summary>
public interface IDocumentIndexRepository : IBaseRepository<DocumentSource>
{
    /// <summary>Every indexed file, tracked, without its chunks.</summary>
    Task<IReadOnlyList<DocumentSource>> ListSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>The tracked source for a relative path, or null.</summary>
    Task<DocumentSource?> FindByPathAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Stages the deletion of the source's stored chunks and the insertion of <paramref name="chunks"/>, so one
    /// save swaps them. A new source is staged for insertion with its chunks.</summary>
    Task ReplaceChunksAsync(DocumentSource source, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>Stages the deletion of the sources with these paths; their chunks are deleted with them.</summary>
    Task RemoveByPathsAsync(IReadOnlyCollection<string> relativePaths, CancellationToken cancellationToken = default);

    /// <summary>Every stored chunk with its document, for loading the in-memory search index.</summary>
    IAsyncEnumerable<DocumentChunkRow> StreamChunksAsync(CancellationToken cancellationToken = default);
}
