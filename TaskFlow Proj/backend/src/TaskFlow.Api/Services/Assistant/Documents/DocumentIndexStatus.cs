namespace TaskFlow.Api.Services.Assistant.Documents;

/// <summary>What the administrator's Document search card shows. <see cref="LastError"/> is fixed or exception text about a
/// file or the embedding server, never document text.</summary>
public record DocumentIndexState(
    bool Enabled,
    string SourcePath,
    bool FolderFound,
    string EmbeddingModel,
    int FileCount,
    int ChunkCount,
    DateTime? LastIndexedAt,
    bool IsRunning,
    string? LastError,
    string? LastErrorFile);

/// <summary>The current state of the document index, shared by the indexer, the search and the status endpoint.</summary>
public class DocumentIndexStatus
{
    private readonly object _gate = new();
    private DocumentIndexState _state = new(true, string.Empty, true, string.Empty, 0, 0, null, false, null, null);

    public DocumentIndexState Current
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public void Update(Func<DocumentIndexState, DocumentIndexState> change)
    {
        lock (_gate)
        {
            _state = change(_state);
        }
    }
}
