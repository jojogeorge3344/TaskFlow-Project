using TaskFlow.Api.Services.Assistant.Documents.Rules;

namespace TaskFlow.Api.Services.Assistant.Documents;

/// <summary>A chunk held in memory for search. <see cref="Vector"/> is L2-normalised.</summary>
public record IndexedChunk(int ChunkId, string RelativePath, string Title, string HeadingPath, string Text, float[] Vector);

public record ScoredChunk(IndexedChunk Chunk, double Score);

/// <summary>Every stored chunk's vector in memory, searched by brute force (design D4): a few thousand dot products per
/// query. The set is replaced as a whole after an indexing run, so a search always sees one consistent set.</summary>
public class DocumentVectorIndex
{
    private sealed record Snapshot(IReadOnlyList<IndexedChunk> Chunks, string? Model)
    {
        private Dictionary<int, IndexedChunk>? _byId;

        public Dictionary<int, IndexedChunk> ById => _byId ??= Chunks.ToDictionary(chunk => chunk.ChunkId);
    }

    private volatile Snapshot _snapshot = new([], null);

    public int Count => _snapshot.Chunks.Count;

    /// <summary>The embedding model the loaded chunks were made with, or null before the first load.</summary>
    public string? Model => _snapshot.Model;

    public void Replace(IReadOnlyList<IndexedChunk> chunks, string model) => _snapshot = new Snapshot(chunks.ToArray(), model);

    /// <summary>The best chunks with a score of at least <paramref name="minScore"/>, best first: at most
    /// <paramref name="topK"/>, and only as many as fit in <paramref name="maxCharacters"/> of text. The best match is
    /// always kept.</summary>
    public IReadOnlyList<ScoredChunk> Search(float[] query, int topK, double minScore, int maxCharacters) =>
        ChunkSelection.Take(Similarities(query, minScore), topK, maxCharacters);

    /// <summary>Every chunk with a similarity of at least <paramref name="floor"/>, best first, then by chunk id. A chunk
    /// whose vector has another length than the query is skipped.</summary>
    public IReadOnlyList<ScoredChunk> Similarities(float[] query, double floor)
    {
        var best = new List<ScoredChunk>();
        foreach (var chunk in _snapshot.Chunks)
        {
            if (chunk.Vector.Length != query.Length)
            {
                continue;
            }

            var score = Dot(chunk.Vector, query);
            if (score >= floor)
            {
                best.Add(new ScoredChunk(chunk, score));
            }
        }

        return best.OrderByDescending(scored => scored.Score).ThenBy(scored => scored.Chunk.ChunkId).ToList();
    }

    /// <summary>The loaded chunks with these ids, in the given order. An id that is not loaded (the index is being
    /// replaced) is skipped.</summary>
    public IReadOnlyList<IndexedChunk> Resolve(IEnumerable<int> chunkIds)
    {
        var byId = _snapshot.ById;
        return chunkIds.Select(id => byId.GetValueOrDefault(id)).OfType<IndexedChunk>().ToList();
    }

    private static double Dot(float[] left, float[] right)
    {
        double sum = 0;
        for (var index = 0; index < left.Length; index++)
        {
            sum += left[index] * right[index];
        }

        return sum;
    }
}
