using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using Task = System.Threading.Tasks.Task;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Assistant.Documents;

public class DocumentIndexRepository : BaseRepository<DocumentSource>, IDocumentIndexRepository
{
    public DocumentIndexRepository(AppDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<DocumentSource>> ListSourcesAsync(CancellationToken cancellationToken = default) =>
        await Set.OrderBy(source => source.RelativePath).ToListAsync(cancellationToken);

    public Task<DocumentSource?> FindByPathAsync(string relativePath, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(source => source.RelativePath == relativePath, cancellationToken);

    public async Task ReplaceChunksAsync(DocumentSource source, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        if (source.Id == 0)
        {
            source.Chunks = chunks.ToList();
            Set.Add(source);
            return;
        }

        // Only the keys are read: the old chunks are deleted by key, without loading their text and vectors.
        var oldIds = await Db.DocumentChunks.Where(chunk => chunk.DocumentSourceId == source.Id).Select(chunk => chunk.Id).ToListAsync(cancellationToken);
        foreach (var id in oldIds)
        {
            Db.Entry(new DocumentChunk { Id = id }).State = EntityState.Deleted;
        }

        foreach (var chunk in chunks)
        {
            chunk.DocumentSourceId = source.Id;
            Db.DocumentChunks.Add(chunk);
        }
    }

    public async Task RemoveByPathsAsync(IReadOnlyCollection<string> relativePaths, CancellationToken cancellationToken = default)
    {
        if (relativePaths.Count == 0)
        {
            return;
        }

        var sources = await Set.Where(source => relativePaths.Contains(source.RelativePath)).ToListAsync(cancellationToken);
        Set.RemoveRange(sources);
    }

    public async IAsyncEnumerable<DocumentChunkRow> StreamChunksAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rows = Db.DocumentChunks.AsNoTracking()
            .OrderBy(chunk => chunk.DocumentSource!.RelativePath).ThenBy(chunk => chunk.Ordinal)
            .Select(chunk => new DocumentChunkRow(
                chunk.Id,
                chunk.DocumentSource!.RelativePath,
                chunk.DocumentSource.Title,
                chunk.DocumentSource.EmbeddingModel,
                chunk.HeadingPath,
                chunk.Text,
                chunk.Embedding,
                chunk.Dimensions))
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken);

        await foreach (var row in rows)
        {
            yield return row;
        }
    }
}
