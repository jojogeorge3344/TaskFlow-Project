namespace TaskFlow.Api.Services.Assistant.Documents;

/// <summary>A chunk held in memory for search. <see cref="Vector"/> is L2-normalised.</summary>
public record IndexedChunk(int ChunkId, string RelativePath, string Title, string HeadingPath, string Text, float[] Vector);

public record ScoredChunk(IndexedChunk Chunk, double Score);

/// <summary>Every stored chunk's vector in memory, searched by brute force (design D4): a few thousand dot products per
/// query. The set is replaced as a whole after an indexing run, so a search always sees one consistent set.</summary>
public class DocumentVectorIndex
{
    private sealed record Snapshot(IReadOnlyList<IndexedChunk> Chunks, string? Model);

    private volatile Snapshot _snapshot = new([], null);

    public int Count => _snapshot.Chunks.Count;

    /// <summary>The embedding model the loaded chunks were made with, or null before the first load.</summary>
    public string? Model => _snapshot.Model;

    public void Replace(IReadOnlyList<IndexedChunk> chunks, string model) => _snapshot = new Snapshot(chunks.ToArray(), model);

    /// <summary>The best chunks with a score of at least <paramref name="minScore"/>, best first: at most
    /// <paramref name="topK"/>, and only as many as fit in <paramref name="maxCharacters"/> of text. The best match is
    /// always kept.</summary>
    public IReadOnlyList<ScoredChunk> Search(float[] query, int topK, double minScore, int maxCharacters)
    {
        var snapshot = _snapshot;
        var best = new List<ScoredChunk>();

        foreach (var chunk in snapshot.Chunks)
        {
            if (chunk.Vector.Length != query.Length)
            {
                continue;
            }

            var score = Dot(chunk.Vector, query);
            if (score >= minScore)
            {
                best.Add(new ScoredChunk(chunk, score));
            }
        }

        var results = new List<ScoredChunk>();
        var characters = 0;
        foreach (var candidate in best.OrderByDescending(scored => scored.Score).ThenBy(scored => scored.Chunk.ChunkId).Take(topK))
        {
            if (results.Count > 0 && characters + candidate.Chunk.Text.Length > maxCharacters)
            {
                continue;
            }

            results.Add(candidate);
            characters += candidate.Chunk.Text.Length;
        }

        return results;
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
