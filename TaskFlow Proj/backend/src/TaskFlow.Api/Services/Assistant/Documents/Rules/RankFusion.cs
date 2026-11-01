namespace TaskFlow.Api.Services.Assistant.Documents.Rules;

/// <summary>The thresholds that decide which chunks may reach the model (design H4 of tfa-hybrid-document-search).
/// <paramref name="KeywordAlone"/> lets a keyword match through on its own: the message has an exact identifier, or the
/// message could not be embedded.</summary>
public sealed record FusionRules(double MinScore, double KeywordAssistMinScore, int RrfK, bool KeywordAlone);

/// <summary>Merges the vector and keyword rankings with Reciprocal Rank Fusion: a chunk scores 1 / (k + rank) for each
/// list it is in, rank from 1, so scores on different scales (cosine, bm25) never have to be compared.</summary>
public static class RankFusion
{
    /// <param name="vectorRanked">Chunks by similarity, best first (each at or above the keyword-assist minimum).</param>
    /// <param name="keywordRanked">Chunks matching the keyword query, best first.</param>
    /// <param name="similarity">The similarity of a chunk id, or null when it is below the keyword-assist minimum or the
    /// message could not be embedded.</param>
    /// <returns>The eligible chunks, best first by fused score, then by chunk id.</returns>
    public static IReadOnlyList<ScoredChunk> Fuse(
        IReadOnlyList<ScoredChunk> vectorRanked,
        IReadOnlyList<IndexedChunk> keywordRanked,
        Func<int, double?> similarity,
        FusionRules rules)
    {
        var chunks = new Dictionary<int, IndexedChunk>();
        var scores = new Dictionary<int, double>();
        var keywordIds = new HashSet<int>();

        for (var index = 0; index < vectorRanked.Count; index++)
        {
            var chunk = vectorRanked[index].Chunk;
            chunks[chunk.ChunkId] = chunk;
            scores[chunk.ChunkId] = scores.GetValueOrDefault(chunk.ChunkId) + 1.0 / (rules.RrfK + index + 1);
        }

        for (var index = 0; index < keywordRanked.Count; index++)
        {
            var chunk = keywordRanked[index];
            chunks[chunk.ChunkId] = chunk;
            keywordIds.Add(chunk.ChunkId);
            scores[chunk.ChunkId] = scores.GetValueOrDefault(chunk.ChunkId) + 1.0 / (rules.RrfK + index + 1);
        }

        return scores
            .Where(entry => IsEligible(similarity(entry.Key), keywordIds.Contains(entry.Key), rules))
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key)
            .Select(entry => new ScoredChunk(chunks[entry.Key], entry.Value))
            .ToList();
    }

    /// <summary>The spec's eligibility rule: similar enough on its own; or a keyword match that is at least loosely
    /// similar; or a keyword match that may stand alone.</summary>
    public static bool IsEligible(double? similarity, bool keywordMatch, FusionRules rules) =>
        similarity >= rules.MinScore
        || (keywordMatch && similarity >= rules.KeywordAssistMinScore)
        || (keywordMatch && rules.KeywordAlone);
}

/// <summary>Picks the chunks passed to the model from a ranking: at most <c>topK</c>, and only as many as fit in
/// <c>maxCharacters</c> of text, skipping a chunk that does not fit. The best match is always kept.</summary>
public static class ChunkSelection
{
    public static IReadOnlyList<ScoredChunk> Take(IEnumerable<ScoredChunk> ranked, int topK, int maxCharacters)
    {
        var results = new List<ScoredChunk>();
        var characters = 0;
        foreach (var candidate in ranked.Take(topK))
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
}
