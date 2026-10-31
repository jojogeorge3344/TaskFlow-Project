using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Config;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Api.Services.Assistant.Documents;

/// <summary>Keeps the TFA document index in step with the documents folder (design D7): it loads the stored index at
/// startup, indexes the folder shortly after, then checks it every <c>RescanMinutes</c> and whenever an administrator asks.
/// Only one run happens at a time; a request made while a run is in progress is ignored.</summary>
public class DocumentIndexerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DocumentKnowledgeOptions _options;
    private readonly DocumentIndexStatus _status;
    private readonly ILogger<DocumentIndexerService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private int _pending;

    public DocumentIndexerService(
        IServiceScopeFactory scopeFactory,
        IOptions<DocumentKnowledgeOptions> options,
        IHostEnvironment environment,
        DocumentIndexStatus status,
        ILogger<DocumentIndexerService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _status = status;
        _logger = logger;
        _status.Update(state => state with
        {
            Enabled = _options.Enabled,
            SourcePath = _options.ResolveSourcePath(environment.ContentRootPath),
            EmbeddingModel = _options.EmbeddingModel.Trim()
        });
    }

    /// <summary>How long after startup the first run waits, so startup itself stays quick.</summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Asks for a run now. Returns false, and does nothing, when indexing is off or a run is already in progress
    /// or already requested.</summary>
    public bool RequestReindex()
    {
        if (!_options.Enabled || _gate.CurrentCount == 0 || _status.Current.IsRunning
            || Interlocked.CompareExchange(ref _pending, 1, 0) != 0)
        {
            return false;
        }

        _requests.Writer.TryWrite(true);
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("TFA document indexing is disabled by configuration.");
            return;
        }

        try
        {
            // Questions can be answered from the stored index while the folder is checked.
            await RunInScopeAsync(indexer => indexer.ReloadIndexAsync(stoppingToken), stoppingToken);

            await WaitAsync(StartDelay, stoppingToken);
            var interval = TimeSpan.FromMinutes(_options.RescanMinutes);
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                await WaitAsync(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    /// <summary>One indexing run, unless one is already in progress. Errors are logged, never thrown, so a failed run never
    /// stops the service.</summary>
    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        try
        {
            _requests.Reader.TryRead(out _);
            await RunInScopeAsync(indexer => indexer.RunAsync(cancellationToken), cancellationToken);
            return true;
        }
        finally
        {
            // A request accepted before this run started is answered by it.
            Interlocked.Exchange(ref _pending, 0);
            _gate.Release();
        }
    }

    private async Task RunInScopeAsync(Func<DocumentIndexer, Task> work, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await work(scope.ServiceProvider.GetRequiredService<DocumentIndexer>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "TFA document indexing failed.");
            _status.Update(state => state with { IsRunning = false, LastError = "Indexing failed. See the API log for details.", LastErrorFile = null });
        }
    }

    /// <summary>Waits for <paramref name="delay"/> or until a run is requested, whichever comes first.</summary>
    private async Task WaitAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(delay);
        try
        {
            await _requests.Reader.WaitToReadAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // The delay passed.
        }
    }

    public override void Dispose()
    {
        _gate.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
