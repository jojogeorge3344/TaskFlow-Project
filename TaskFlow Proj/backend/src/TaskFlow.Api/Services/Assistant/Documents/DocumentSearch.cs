using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Config;
using TaskFlow.Api.Integrations.Ai.Embeddings;
using TaskFlow.Api.Repositories.Assistant.Documents;
using TaskFlow.Api.Services.Assistant.Documents.Rules;

namespace TaskFlow.Api.Services.Assistant.Documents;

/// <summary>Searches the documents for a message by similarity (in-memory index) and by keyword (the FTS5 index), and
/// merges the two (design H4 of tfa-hybrid-document-search). Runs in code before the model is called, so it works with
/// every provider and every model, including ones that cannot call tools.</summary>
public class DocumentSearch : IDocumentSearch
{
    /// <summary>A message shorter than this that finds nothing on its own is searched again together with the previous user
    /// message.</summary>
    public const int ShortFollowUpLength = 40;

    private readonly DocumentVectorIndex _index;
    private readonly DocumentIndexStatus _status;
    private readonly IEmbeddingClient _embeddings;
    private readonly DocumentEmbeddingEndpoint _endpoint;
    private readonly IDocumentIndexRepository _repository;
    private readonly DocumentKnowledgeOptions _options;
    private readonly ILogger<DocumentSearch> _logger;

    public DocumentSearch(
        DocumentVectorIndex index,
        DocumentIndexStatus status,
        IEmbeddingClient embeddings,
        DocumentEmbeddingEndpoint endpoint,
        IDocumentIndexRepository repository,
        IOptions<DocumentKnowledgeOptions> options,
        ILogger<DocumentSearch> logger)
    {
        _index = index;
        _status = status;
        _embeddings = embeddings;
        _endpoint = endpoint;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DocumentContext> SearchAsync(string message, string? previousUserMessage, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return DocumentContext.Unavailable(DocumentUnavailableReason.CannotSearch);
        }

        if (_index.Count == 0 || _index.Model != _options.IndexModel)
        {
            var status = _status.Current;
            return DocumentContext.Unavailable(status.IsRunning || status.LastIndexedAt is null
                ? DocumentUnavailableReason.Preparing
                : DocumentUnavailableReason.CannotSearch);
        }

        // A short message may be a follow-up ("and for managers?") or a complete question ("How do I mark notifications as
        // read?"). It is searched on its own first; the previous user message is added only when that finds nothing (design D9).
        var queries = new List<string> { message.Trim() };
        if (FollowUpQuery(message, previousUserMessage) is { } followUp)
        {
            queries.Add(followUp);
        }

        float[][]? vectors = null;
        try
        {
            var endpoint = await _endpoint.ResolveAsync(cancellationToken);
            vectors = await _embeddings.EmbedAsync(endpoint, queries, EmbeddingPurpose.Query, cancellationToken);
        }
        catch (EmbeddingUnavailableException unavailable)
        {
            _logger.LogWarning("TFA could not search the documents: {Reason}", unavailable.Message);
            if (!_options.HybridSearch)
            {
                return DocumentContext.Unavailable(DocumentUnavailableReason.CannotSearch);
            }
        }

        for (var index = 0; index < queries.Count; index++)
        {
            var chunks = vectors is null
                ? await KeywordOnlyAsync(queries[index], cancellationToken)
                : _options.HybridSearch
                    ? await HybridAsync(queries[index], vectors[index], cancellationToken)
                    : _index.Search(vectors[index], _options.TopK, _options.MinScore, _options.MaxContextCharacters);

            if (chunks.Count > 0)
            {
                return DocumentContext.Found(chunks);
            }
        }

        // Without embeddings an empty keyword search does not show the documents have nothing on the subject.
        return vectors is null ? DocumentContext.Unavailable(DocumentUnavailableReason.CannotSearch) : DocumentContext.NoMatch;
    }

    private async Task<IReadOnlyList<ScoredChunk>> HybridAsync(string query, float[] vector, CancellationToken cancellationToken)
    {
        var keyword = KeywordQuery.Build(query);
        var similar = _index.Similarities(vector, Math.Min(_options.KeywordAssistMinScore, _options.MinScore));
        var similarity = similar.ToDictionary(scored => scored.Chunk.ChunkId, scored => scored.Score);

        var fused = RankFusion.Fuse(
            similar.Take(_options.CandidateCount).ToList(),
            await KeywordRankedAsync(keyword, cancellationToken),
            id => similarity.TryGetValue(id, out var score) ? score : null,
            Rules(keywordAlone: keyword.HasExactIdentifier));

        return ChunkSelection.Take(fused, _options.TopK, _options.MaxContextCharacters);
    }

    /// <summary>Used when the message cannot be embedded: every keyword match may reach the model (design H5).</summary>
    private async Task<IReadOnlyList<ScoredChunk>> KeywordOnlyAsync(string query, CancellationToken cancellationToken)
    {
        var fused = RankFusion.Fuse([], await KeywordRankedAsync(KeywordQuery.Build(query), cancellationToken), _ => null, Rules(keywordAlone: true));
        return ChunkSelection.Take(fused, _options.TopK, _options.MaxContextCharacters);
    }

    private async Task<IReadOnlyList<IndexedChunk>> KeywordRankedAsync(KeywordQuery keyword, CancellationToken cancellationToken) =>
        keyword.FtsQuery is null
            ? []
            : _index.Resolve(await _repository.SearchKeywordAsync(keyword.FtsQuery, _options.CandidateCount, cancellationToken));

    private FusionRules Rules(bool keywordAlone) =>
        new(_options.MinScore, _options.KeywordAssistMinScore, _options.RrfK, keywordAlone);

    /// <summary>The message with the previous user message in front, for a short message that may be a follow-up; null when
    /// the message is not short or there is no previous message.</summary>
    public static string? FollowUpQuery(string message, string? previousUserMessage)
    {
        var text = message.Trim();
        return text.Length < ShortFollowUpLength && !string.IsNullOrWhiteSpace(previousUserMessage)
            ? $"{previousUserMessage.Trim()}\n{text}"
            : null;
    }
}
