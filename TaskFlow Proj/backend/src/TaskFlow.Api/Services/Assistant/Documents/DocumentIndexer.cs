using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Config;
using TaskFlow.Api.Integrations.Ai.Embeddings;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Assistant.Documents;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Services.Assistant.Documents.Rules;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Api.Services.Assistant.Documents;

/// <summary>What one indexing run did.</summary>
public record DocumentIndexRun(int Embedded, int Skipped, int Removed, int Failed);

/// <summary>Brings the stored index in step with the documents folder (design D6): unchanged files are skipped by hash and
/// model, changed files are re-embedded and swapped in one save, deleted files are removed, and the in-memory index is
/// reloaded when anything changed. One bad file does not stop the others; an unreachable embedding server stops the run.</summary>
public class DocumentIndexer
{
    /// <summary>During a run, the in-memory index is refreshed after this many newly embedded files.</summary>
    public const int ReloadEvery = 10;

    private readonly IDocumentIndexRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmbeddingClient _embeddings;
    private readonly DocumentEmbeddingEndpoint _endpoint;
    private readonly DocumentVectorIndex _index;
    private readonly DocumentIndexStatus _status;
    private readonly DocumentKnowledgeOptions _options;
    private readonly string _folder;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DocumentIndexer> _logger;

    public DocumentIndexer(
        IDocumentIndexRepository repository,
        IUnitOfWork unitOfWork,
        IEmbeddingClient embeddings,
        DocumentEmbeddingEndpoint endpoint,
        DocumentVectorIndex index,
        DocumentIndexStatus status,
        IOptions<DocumentKnowledgeOptions> options,
        IHostEnvironment environment,
        TimeProvider timeProvider,
        ILogger<DocumentIndexer> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _embeddings = embeddings;
        _endpoint = endpoint;
        _index = index;
        _status = status;
        _options = options.Value;
        _folder = _options.ResolveSourcePath(environment.ContentRootPath);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private string Model => _options.EmbeddingModel.Trim();

    public async Task<DocumentIndexRun> RunAsync(CancellationToken cancellationToken = default)
    {
        _status.Update(state => state with
        {
            Enabled = true,
            SourcePath = _folder,
            EmbeddingModel = Model,
            IsRunning = true,
            LastError = null,
            LastErrorFile = null
        });

        var embedded = 0;
        var skipped = 0;
        var failed = 0;
        string? error = null;
        string? errorFile = null;

        try
        {
            var folderFound = Directory.Exists(_folder);
            var files = folderFound ? FindMarkdownFiles() : [];
            if (!folderFound)
            {
                error = $"The documents folder was not found: {_folder}";
                _logger.LogWarning("The TFA documents folder was not found: {Folder}", _folder);
            }

            var sources = (await _repository.ListSourcesAsync(cancellationToken)).ToDictionary(source => source.RelativePath, StringComparer.Ordinal);
            EmbeddingEndpoint? endpoint = null;

            foreach (var (relativePath, fullPath) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
                    var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                    sources.TryGetValue(relativePath, out var source);
                    if (source is not null && source.ContentHash == hash && source.EmbeddingModel == Model)
                    {
                        skipped++;
                        continue;
                    }

                    endpoint ??= await _endpoint.ResolveAsync(cancellationToken);
                    var document = MarkdownChunker.Chunk(Encoding.UTF8.GetString(bytes), Path.GetFileName(fullPath), _options.MaxChunkCharacters, _options.ExcludedHeadings);
                    var vectors = document.Chunks.Count == 0
                        ? []
                        : await _embeddings.EmbedAsync(endpoint, document.Chunks.Select(chunk => chunk.EmbeddingText).ToList(), EmbeddingPurpose.Document, cancellationToken);

                    source ??= new DocumentSource { RelativePath = relativePath };
                    source.Title = Truncate(document.Title, 300);
                    source.ContentHash = hash;
                    source.EmbeddingModel = Model;
                    source.ChunkCount = document.Chunks.Count;
                    source.IndexedAt = _timeProvider.GetUtcNow().UtcDateTime;

                    // The old chunks stay stored, and in the in-memory index, until this one save replaces them.
                    await _repository.ReplaceChunksAsync(
                        source,
                        document.Chunks.Select((chunk, position) => new DocumentChunk
                        {
                            Ordinal = chunk.Ordinal,
                            HeadingPath = chunk.HeadingPath,
                            Text = chunk.Text,
                            Embedding = EmbeddingVectors.ToBytes(vectors[position]),
                            Dimensions = vectors[position].Length
                        }).ToList(),
                        cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    sources[relativePath] = source;
                    embedded++;

                    // A long first run makes its progress searchable as it goes.
                    if (embedded % ReloadEvery == 0)
                    {
                        await ReloadIndexAsync(cancellationToken);
                    }
                }
                catch (EmbeddingUnavailableException unavailable)
                {
                    // Every remaining file would fail the same way.
                    failed++;
                    error = unavailable.Message;
                    errorFile = relativePath;
                    _logger.LogWarning("TFA document indexing stopped: {Reason}", unavailable.Message);
                    break;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failed++;
                    error = $"{relativePath} could not be indexed: {exception.Message}";
                    errorFile = relativePath;
                    _logger.LogWarning(exception, "TFA could not index the document {File}", relativePath);

                    // Forget anything half-staged for this file, and read the sources again so later saves stay correct.
                    _unitOfWork.DetachAll();
                    sources = (await _repository.ListSourcesAsync(cancellationToken)).ToDictionary(source => source.RelativePath, StringComparer.Ordinal);
                }
            }

            var onDisk = files.Select(file => file.RelativePath).ToHashSet(StringComparer.Ordinal);
            var removed = sources.Keys.Where(path => !onDisk.Contains(path)).ToList();
            if (removed.Count > 0)
            {
                await _repository.RemoveByPathsAsync(removed, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (embedded > 0 || removed.Count > 0 || _index.Model != Model)
            {
                await ReloadIndexAsync(cancellationToken);
            }

            var stored = (await _repository.ListSourcesAsync(cancellationToken)).Count(source => source.EmbeddingModel == Model);
            _status.Update(state => state with
            {
                FolderFound = folderFound,
                FileCount = stored,
                ChunkCount = _index.Count,
                LastIndexedAt = _timeProvider.GetUtcNow().UtcDateTime,
                LastError = error,
                LastErrorFile = errorFile
            });

            if (embedded > 0 || removed.Count > 0)
            {
                _logger.LogInformation(
                    "TFA document index updated: {Embedded} embedded, {Skipped} unchanged, {Removed} removed, {Failed} failed",
                    embedded, skipped, removed.Count, failed);
            }

            return new DocumentIndexRun(embedded, skipped, removed.Count, failed);
        }
        finally
        {
            _status.Update(state => state with { IsRunning = false });
        }
    }

    /// <summary>Loads the stored chunks of the configured model into the in-memory index, replacing what it held.</summary>
    public async Task ReloadIndexAsync(CancellationToken cancellationToken = default)
    {
        var chunks = new List<IndexedChunk>();
        await foreach (var row in _repository.StreamChunksAsync(cancellationToken))
        {
            if (row.EmbeddingModel == Model && EmbeddingVectors.FromBytes(row.Embedding, row.Dimensions) is { } vector)
            {
                chunks.Add(new IndexedChunk(row.ChunkId, row.RelativePath, row.Title, row.HeadingPath, row.Text, vector));
            }
        }

        _index.Replace(chunks, Model);

        // After a restart the card shows what is stored, not zeros, until the first run of this process finishes.
        var sources = (await _repository.ListSourcesAsync(cancellationToken)).Where(source => source.EmbeddingModel == Model).ToList();
        _status.Update(state => state with
        {
            ChunkCount = chunks.Count,
            FileCount = sources.Count,
            LastIndexedAt = state.LastIndexedAt ?? (sources.Count == 0 ? null : sources.Max(source => source.IndexedAt))
        });
    }

    private List<(string RelativePath, string FullPath)> FindMarkdownFiles() =>
        Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories)
            .Where(file => string.Equals(Path.GetExtension(file), ".md", StringComparison.OrdinalIgnoreCase))
            .Select(file => (Path.GetRelativePath(_folder, file).Replace('\\', '/'), file))
            .OrderBy(file => file.Item1, StringComparer.Ordinal)
            .ToList();

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];
}
