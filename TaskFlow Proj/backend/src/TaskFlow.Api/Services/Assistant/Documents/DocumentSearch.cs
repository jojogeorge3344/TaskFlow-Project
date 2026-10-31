using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Config;
using TaskFlow.Api.Integrations.Ai.Embeddings;

namespace TaskFlow.Api.Services.Assistant.Documents;

/// <summary>Embeds the message locally and searches the in-memory index. Runs in code before the model is called, so it
/// works with every provider and every model, including ones that cannot call tools.</summary>
public class DocumentSearch : IDocumentSearch
{
    /// <summary>A message shorter than this that finds nothing on its own is searched again together with the previous user
    /// message.</summary>
    public const int ShortFollowUpLength = 40;

    private readonly DocumentVectorIndex _index;
    private readonly DocumentIndexStatus _status;
    private readonly IEmbeddingClient _embeddings;
    private readonly DocumentEmbeddingEndpoint _endpoint;
    private readonly DocumentKnowledgeOptions _options;
    private readonly ILogger<DocumentSearch> _logger;

    public DocumentSearch(
        DocumentVectorIndex index,
        DocumentIndexStatus status,
        IEmbeddingClient embeddings,
        DocumentEmbeddingEndpoint endpoint,
        IOptions<DocumentKnowledgeOptions> options,
        ILogger<DocumentSearch> logger)
    {
        _index = index;
        _status = status;
        _embeddings = embeddings;
        _endpoint = endpoint;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DocumentContext> SearchAsync(string message, string? previousUserMessage, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return DocumentContext.Unavailable(DocumentUnavailableReason.CannotSearch);
        }

        if (_index.Count == 0 || _index.Model != _options.EmbeddingModel.Trim())
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

        float[][] vectors;
        try
        {
            var endpoint = await _endpoint.ResolveAsync(cancellationToken);
            vectors = await _embeddings.EmbedAsync(endpoint, queries, EmbeddingPurpose.Query, cancellationToken);
        }
        catch (EmbeddingUnavailableException unavailable)
        {
            _logger.LogWarning("TFA could not search the documents: {Reason}", unavailable.Message);
            return DocumentContext.Unavailable(DocumentUnavailableReason.CannotSearch);
        }

        foreach (var vector in vectors)
        {
            var chunks = _index.Search(vector, _options.TopK, _options.MinScore, _options.MaxContextCharacters);
            if (chunks.Count > 0)
            {
                return DocumentContext.Found(chunks);
            }
        }

        return DocumentContext.NoMatch;
    }

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
