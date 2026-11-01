using TaskFlow.Api.Services.Assistant.Documents;
using TaskFlow.Api.Services.Assistant.Documents.Rules;

namespace TaskFlow.UnitTests.Assistant.Documents;

public class DocumentVectorIndexTests
{
    private static float[] Unit(params float[] values)
    {
        var length = (float)Math.Sqrt(values.Sum(value => value * value));
        return values.Select(value => value / length).ToArray();
    }

    private static IndexedChunk Chunk(int id, float[] vector, string text = "text", string path = "a.md") =>
        new(id, path, "Title", $"Section {id}", text, vector);

    [Fact]
    public void ResultsAreOrderedByScore()
    {
        var index = new DocumentVectorIndex();
        index.Replace([Chunk(1, Unit(0, 1)), Chunk(2, Unit(1, 0)), Chunk(3, Unit(1, 1))], "m");

        var results = index.Search(Unit(1, 0.1f), topK: 3, minScore: 0, maxCharacters: 1000);

        Assert.Equal([2, 3, 1], results.Select(result => result.Chunk.ChunkId));
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void ChunksBelowTheMinimumScoreAreLeftOut()
    {
        var index = new DocumentVectorIndex();
        index.Replace([Chunk(1, Unit(1, 0)), Chunk(2, Unit(0, 1))], "m");

        var results = index.Search(Unit(1, 0), topK: 5, minScore: 0.5, maxCharacters: 1000);

        Assert.Equal([1], results.Select(result => result.Chunk.ChunkId));
    }

    [Fact]
    public void AtMostTopKChunksAreReturned()
    {
        var index = new DocumentVectorIndex();
        index.Replace(Enumerable.Range(1, 10).Select(id => Chunk(id, Unit(1, id / 100f))).ToList(), "m");

        Assert.Equal(3, index.Search(Unit(1, 0), topK: 3, minScore: 0, maxCharacters: 10_000).Count);
    }

    [Fact]
    public void TheCharacterBudgetKeepsTheBestThatFit()
    {
        var index = new DocumentVectorIndex();
        index.Replace(
        [
            Chunk(1, Unit(1, 0), new string('a', 600)),
            Chunk(2, Unit(1, 0.1f), new string('b', 600)),
            Chunk(3, Unit(1, 0.2f), new string('c', 300))
        ], "m");

        var results = index.Search(Unit(1, 0), topK: 5, minScore: 0, maxCharacters: 1000);

        Assert.Equal([1, 3], results.Select(result => result.Chunk.ChunkId));
    }

    [Fact]
    public void TheBestMatchIsKeptEvenWhenItAloneIsOverTheBudget()
    {
        var index = new DocumentVectorIndex();
        index.Replace([Chunk(1, Unit(1, 0), new string('a', 2000))], "m");

        Assert.Single(index.Search(Unit(1, 0), topK: 5, minScore: 0, maxCharacters: 1000));
    }

    [Fact]
    public void AnEmptyIndexFindsNothing()
    {
        var index = new DocumentVectorIndex();

        Assert.Empty(index.Search(Unit(1, 0), topK: 5, minScore: 0, maxCharacters: 1000));
        Assert.Equal(0, index.Count);
        Assert.Null(index.Model);
    }

    [Fact]
    public void VectorsOfAnotherSizeAreIgnored()
    {
        var index = new DocumentVectorIndex();
        index.Replace([Chunk(1, Unit(1, 0, 0)), Chunk(2, Unit(1, 0))], "m");

        Assert.Equal([2], index.Search(Unit(1, 0), topK: 5, minScore: 0, maxCharacters: 1000).Select(result => result.Chunk.ChunkId));
    }

    [Fact]
    public async Task ASwapDuringSearchesAlwaysGivesResultsFromOneSet()
    {
        var index = new DocumentVectorIndex();
        var first = Enumerable.Range(1, 200).Select(id => Chunk(id, Unit(1, id / 1000f), path: "first.md")).ToList();
        var second = Enumerable.Range(1, 200).Select(id => Chunk(id, Unit(1, id / 1000f), path: "second.md")).ToList();
        index.Replace(first, "m");

        using var stop = new CancellationTokenSource();
        var swapper = Task.Run(() =>
        {
            for (var turn = 0; !stop.IsCancellationRequested; turn++)
            {
                index.Replace(turn % 2 == 0 ? second : first, "m");
            }
        });

        var searches = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            var mixed = 0;
            for (var search = 0; search < 300; search++)
            {
                var paths = index.Search(Unit(1, 0), topK: 20, minScore: 0, maxCharacters: 100_000).Select(result => result.Chunk.RelativePath).Distinct().Count();
                mixed += paths == 1 ? 0 : 1;
            }

            return mixed;
        })).ToList();

        var mixedResults = await Task.WhenAll(searches);
        await stop.CancelAsync();
        await swapper;
        Assert.All(mixedResults, mixed => Assert.Equal(0, mixed));
    }

    [Fact]
    public void VectorsRoundTripThroughTheStoredBytes()
    {
        var vector = Unit(0.25f, -0.5f, 0.75f);

        var bytes = EmbeddingVectors.ToBytes(vector);

        Assert.Equal(12, bytes.Length);
        Assert.Equal(vector, EmbeddingVectors.FromBytes(bytes, 3));
        Assert.Null(EmbeddingVectors.FromBytes(bytes, 4));
    }
}
