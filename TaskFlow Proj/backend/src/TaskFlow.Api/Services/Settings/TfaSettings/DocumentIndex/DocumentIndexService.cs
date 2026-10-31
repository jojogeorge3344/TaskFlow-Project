using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Services.Assistant.Documents;

namespace TaskFlow.Api.Services.Settings.TfaSettings.DocumentIndex;

public class DocumentIndexService : IDocumentIndexService
{
    private readonly DocumentIndexStatus _status;
    private readonly DocumentIndexerService _indexer;

    public DocumentIndexService(DocumentIndexStatus status, DocumentIndexerService indexer)
    {
        _status = status;
        _indexer = indexer;
    }

    public DocumentIndexStatusDto GetStatus() => ToDto(_status.Current, running: false);

    public DocumentIndexStatusDto RequestReindex()
    {
        var current = _status.Current;
        if (!current.Enabled)
        {
            throw new BusinessRuleException("Document search is switched off in the server configuration (DocumentKnowledge:Enabled).");
        }

        // An accepted request starts a run within moments, so the card shows it as running straight away.
        return ToDto(_status.Current, running: _indexer.RequestReindex());
    }

    private static DocumentIndexStatusDto ToDto(DocumentIndexState state, bool running) =>
        new(
            state.Enabled,
            state.SourcePath,
            state.FolderFound,
            state.EmbeddingModel,
            state.FileCount,
            state.ChunkCount,
            state.LastIndexedAt is { } at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null,
            state.IsRunning || running,
            state.LastError,
            state.LastErrorFile);
}
