using TaskFlow.Api.Services.Assistant.Documents;
using TaskFlow.Api.Services.Assistant.Documents.Rules;

namespace TaskFlow.UnitTests.Assistant.Documents;

public class RankFusionTests
{
    private static readonly FusionRules Rules = new(MinScore: 0.65, KeywordAssistMinScore: 0.55, RrfK: 60, KeywordAlone: false);

    private static IndexedChunk Chunk(int id, string text = "text") => new(id, "a.md", "A", $"Section {id}", text, [1f]);

    private static ScoredChunk Scored(int id, double score) => new(Chunk(id), score);

    private static Func<int, double?> Similarities(params (int Id, double Score)[] scores) =>
        id => scores.Where(score => score.Id == id).Select(score => (double?)score.Score).FirstOrDefault();

    [Fact]
    public void AChunkSimilarEnoughOnItsOwnIsEligible()
    {
        var fused = RankFusion.Fuse([Scored(1, 0.7)], [], Similarities((1, 0.7)), Rules);

        Assert.Equal([1], fused.Select(chunk => chunk.Chunk.ChunkId));
    }

    [Fact]
    public void AKeywordMatchLiftsABorderlineChunk()
    {
        var fused = RankFusion.Fuse([Scored(1, 0.6)], [Chunk(1)], Similarities((1, 0.6)), Rules);

        Assert.Equal([1], fused.Select(chunk => chunk.Chunk.ChunkId));
    }

    [Fact]
    public void ABorderlineChunkWithoutAKeywordMatchIsNotEligible()
    {
        Assert.Empty(RankFusion.Fuse([Scored(1, 0.6)], [], Similarities((1, 0.6)), Rules));
    }

    [Fact]
    public void AnOrdinaryKeywordMatchBelowTheAssistMinimumIsNotEligible()
    {
        Assert.Empty(RankFusion.Fuse([], [Chunk(1)], Similarities(), Rules));
    }

    [Fact]
    public void AKeywordMatchStandsAloneForAnExactIdentifierOrWithoutEmbeddings()
    {
        var fused = RankFusion.Fuse([], [Chunk(1)], Similarities(), Rules with { KeywordAlone = true });

        Assert.Equal([1], fused.Select(chunk => chunk.Chunk.ChunkId));
    }

    [Fact]
    public void AChunkInBothListsOutranksChunksInOneList()
    {
        // 1 is top by vector only, 2 is top by keyword only, 3 is second in both.
        var fused = RankFusion.Fuse(
            [Scored(1, 0.9), Scored(3, 0.8)],
            [Chunk(2), Chunk(3)],
            Similarities((1, 0.9), (3, 0.8), (2, 0.7)),
            Rules);

        Assert.Equal([3, 1, 2], fused.Select(chunk => chunk.Chunk.ChunkId));
        Assert.Equal(2.0 / 62, fused[0].Score, precision: 12);
    }

    [Fact]
    public void EqualScoresAreOrderedByChunkId()
    {
        var fused = RankFusion.Fuse([Scored(5, 0.9)], [Chunk(4)], Similarities((5, 0.9), (4, 0.9)), Rules);

        Assert.Equal([4, 5], fused.Select(chunk => chunk.Chunk.ChunkId));
    }

    [Fact]
    public void SelectionKeepsTopKWithinTheCharacterBudgetAndAlwaysTheBest()
    {
        var ranked = new[]
        {
            new ScoredChunk(Chunk(1, new string('a', 500)), 3),
            new ScoredChunk(Chunk(2, new string('b', 300)), 2),
            new ScoredChunk(Chunk(3, new string('c', 100)), 1),
            new ScoredChunk(Chunk(4, "d"), 0.5)
        };

        Assert.Equal([1, 3], ChunkSelection.Take(ranked, topK: 3, maxCharacters: 650).Select(chunk => chunk.Chunk.ChunkId));
        Assert.Equal([1], ChunkSelection.Take(ranked, topK: 3, maxCharacters: 10).Select(chunk => chunk.Chunk.ChunkId));
    }
}
