using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Api.Config;
using TaskFlow.Api.Integrations.Ai.Embeddings;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Assistant.Documents;
using TaskFlow.Api.Repositories.Base;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;
using TaskFlow.Api.Services.Assistant.Documents;

namespace TaskFlow.UnitTests.Assistant.Documents;

public sealed class DocumentIndexerServiceTests : IAsyncDisposable
{
    /// <summary>Counts the scopes the service opens: one to load the stored index, then one per indexing run.</summary>
    private sealed class CountingScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int _scopes;

        public int Scopes => Volatile.Read(ref _scopes);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _scopes);
            return inner.CreateScope();
        }
    }

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"taskflow-docs-{Guid.NewGuid():N}");
    private readonly FakeEmbeddingClient _embeddings = new();
    private readonly ServiceProvider _provider;
    private readonly CountingScopeFactory _scopes;

    public DocumentIndexerServiceTests()
    {
        _connection.Open();
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "task.md"), "# Tasks\n\n## Create\nA task needs a title and a deadline.");

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        services.AddScoped<IDocumentIndexRepository, DocumentIndexRepository>();
        services.AddScoped<IAiProviderConfigRepository, AiProviderConfigRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IEmbeddingClient>(_embeddings);
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { ContentRootPath = Path.GetTempPath() });
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.Configure<DocumentKnowledgeOptions>(options =>
        {
            options.SourcePath = _folder;
            options.RescanMinutes = 60;
        });
        services.AddSingleton<DocumentVectorIndex>();
        services.AddSingleton<DocumentIndexStatus>();
        services.AddScoped<DocumentEmbeddingEndpoint>();
        services.AddScoped<DocumentIndexer>();
        _provider = services.BuildServiceProvider();
        _provider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        _scopes = new CountingScopeFactory(_provider.GetRequiredService<IServiceScopeFactory>());
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
        Directory.Delete(_folder, recursive: true);
    }

    private DocumentIndexerService NewService(bool enabled = true) =>
        new(
            _scopes,
            Microsoft.Extensions.Options.Options.Create(new DocumentKnowledgeOptions { Enabled = enabled, SourcePath = _folder, RescanMinutes = 60 }),
            _provider.GetRequiredService<IHostEnvironment>(),
            _provider.GetRequiredService<DocumentIndexStatus>(),
            NullLogger<DocumentIndexerService>.Instance)
        {
            StartDelay = TimeSpan.FromHours(1)
        };

    private static async Task Eventually(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    [Fact]
    public async Task TwoConcurrentRequestsCauseOneRun()
    {
        var service = NewService();
        await service.StartAsync(CancellationToken.None);
        await Eventually(() => _scopes.Scopes == 1);

        var accepted = await Task.WhenAll(Task.Run(service.RequestReindex), Task.Run(service.RequestReindex));
        await Eventually(() => _provider.GetRequiredService<DocumentIndexStatus>().Current.LastIndexedAt is not null);
        await Task.Delay(300);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, accepted.Count(value => value));
        Assert.Equal(2, _scopes.Scopes);
        Assert.Single(_embeddings.Calls);
    }

    [Fact]
    public async Task ARequestWhileARunIsInProgressIsIgnored()
    {
        var service = NewService();
        _embeddings.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = service.RunOnceAsync();
        await _embeddings.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(_provider.GetRequiredService<DocumentIndexStatus>().Current.IsRunning);
        Assert.False(service.RequestReindex());
        Assert.False(await service.RunOnceAsync());

        _embeddings.Gate.SetResult();
        Assert.True(await run);
        Assert.False(_provider.GetRequiredService<DocumentIndexStatus>().Current.IsRunning);
        Assert.True(service.RequestReindex());
    }

    [Fact]
    public async Task TheStoredIndexIsLoadedAtStartupBeforeTheFolderIsChecked()
    {
        await NewService().RunOnceAsync();
        var index = _provider.GetRequiredService<DocumentVectorIndex>();
        index.Replace([], "something else");
        _embeddings.Calls.Clear();

        var service = NewService();
        await service.StartAsync(CancellationToken.None);
        await Eventually(() => index.Count == 1);
        await service.StopAsync(CancellationToken.None);

        Assert.Empty(_embeddings.Calls);
        Assert.Equal("nomic-embed-text", index.Model);
    }

    [Fact]
    public async Task AfterARestartTheStatusShowsTheStoredFilesAndLastRunBeforeTheFirstRun()
    {
        await NewService().RunOnceAsync();
        var status = _provider.GetRequiredService<DocumentIndexStatus>();
        status.Update(state => state with { FileCount = 0, ChunkCount = 0, LastIndexedAt = null });

        var service = NewService();
        await service.StartAsync(CancellationToken.None);
        await Eventually(() => status.Current.FileCount == 1);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, status.Current.ChunkCount);
        Assert.NotNull(status.Current.LastIndexedAt);
    }

    [Fact]
    public async Task WhenDisabledNothingRunsAndRequestsAreRefused()
    {
        var service = NewService(enabled: false);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(0, _scopes.Scopes);
        Assert.False(service.RequestReindex());
        Assert.False(_provider.GetRequiredService<DocumentIndexStatus>().Current.Enabled);
    }

    [Fact]
    public async Task AMissingFolderDoesNotStopTheService()
    {
        Directory.Delete(_folder, recursive: true);
        var service = NewService();

        Assert.True(await service.RunOnceAsync());

        var status = _provider.GetRequiredService<DocumentIndexStatus>().Current;
        Assert.False(status.FolderFound);
        Assert.Contains("documents folder was not found", status.LastError);
        Directory.CreateDirectory(_folder);
    }
}
