using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Api.Config;
using TaskFlow.Api.Integrations.Ai.Embeddings;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;
using TaskFlow.Api.Services.Assistant.Documents;

namespace TaskFlow.UnitTests.Assistant.Documents;

public sealed class DocumentSearchTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly FakeEmbeddingClient _embeddings = new();
    private readonly DocumentVectorIndex _index = new();
    private readonly DocumentIndexStatus _status = new();
    private readonly DocumentKnowledgeOptions _options = new() { MinScore = 0.5 };

    public DocumentSearchTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _status.Update(state => state with { LastIndexedAt = DateTime.UtcNow });
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private DocumentSearch Search()
    {
        var options = Microsoft.Extensions.Options.Options.Create(_options);
        return new DocumentSearch(_index, _status, _embeddings, new DocumentEmbeddingEndpoint(new AiProviderConfigRepository(_db), options), options, NullLogger<DocumentSearch>.Instance);
    }

    private void Load(params (string Heading, string Text)[] chunks) =>
        _index.Replace(
            chunks.Select((chunk, index) => new IndexedChunk(index + 1, "FRD.md", "FRD", chunk.Heading, chunk.Text, FakeEmbeddingClient.Vector(chunk.Text))).ToList(),
            "nomic-embed-text");

    [Fact]
    public async Task MatchingChunksAreFound()
    {
        Load(("Reminders", "The overdue reminder is sent to the assignee."), ("Mail", "Compose mail screen."));

        var context = await Search().SearchAsync("who receives the overdue reminder?", null);

        Assert.Equal(DocumentContextKind.Found, context.Kind);
        Assert.Equal(["Reminders"], context.Chunks.Select(chunk => chunk.Chunk.HeadingPath));
        var call = Assert.Single(_embeddings.Calls);
        Assert.Equal(EmbeddingPurpose.Query, call.Purpose);
        Assert.Equal("nomic-embed-text", call.Endpoint.Model);
    }

    [Fact]
    public async Task NothingAboveTheMinimumIsNoMatch()
    {
        Load(("Mail", "Compose mail screen."));

        var context = await Search().SearchAsync("what is the capital of France?", null);

        Assert.Equal(DocumentContextKind.NoMatch, context.Kind);
        Assert.Empty(context.Chunks);
    }

    [Fact]
    public async Task AnUnreachableEmbeddingServerIsUnavailable()
    {
        Load(("Mail", "Compose mail screen."));
        _embeddings.FailFor = _ => new EmbeddingUnavailableException(EmbeddingFailureKind.Unreachable, new EmbeddingEndpoint("http://localhost:11434", "nomic-embed-text"));

        var context = await Search().SearchAsync("how do I send mail?", null);

        Assert.Equal((DocumentContextKind.Unavailable, DocumentUnavailableReason.CannotSearch), (context.Kind, context.Reason));
    }

    [Fact]
    public async Task AnEmptyIndexBeforeTheFirstRunIsPreparing()
    {
        _status.Update(state => state with { LastIndexedAt = null });

        var context = await Search().SearchAsync("how do I send mail?", null);

        Assert.Equal((DocumentContextKind.Unavailable, DocumentUnavailableReason.Preparing), (context.Kind, context.Reason));
        Assert.Empty(_embeddings.Calls);
    }

    [Fact]
    public async Task AnEmptyIndexWhileTheFirstRunIsGoingIsPreparing()
    {
        _status.Update(state => state with { IsRunning = true });

        Assert.Equal(DocumentUnavailableReason.Preparing, (await Search().SearchAsync("how do I send mail?", null)).Reason);
    }

    [Fact]
    public async Task AnEmptyIndexAfterARunIsUnavailable()
    {
        Assert.Equal(DocumentUnavailableReason.CannotSearch, (await Search().SearchAsync("how do I send mail?", null)).Reason);
    }

    [Fact]
    public async Task AnIndexOfAnotherModelIsNotSearched()
    {
        Load(("Mail", "Compose mail screen."));
        _options.EmbeddingModel = "mxbai-embed-large";

        var context = await Search().SearchAsync("how do I send mail?", null);

        Assert.Equal(DocumentContextKind.Unavailable, context.Kind);
        Assert.Empty(_embeddings.Calls);
    }

    [Fact]
    public async Task SwitchedOffIsUnavailable()
    {
        Load(("Mail", "Compose mail screen."));
        _options.Enabled = false;

        Assert.Equal(DocumentContextKind.Unavailable, (await Search().SearchAsync("how do I send mail?", null)).Kind);
        Assert.Empty(_embeddings.Calls);
    }

    [Fact]
    public async Task AShortFollowUpThatFindsNothingAloneIsSearchedWithThePreviousQuestion()
    {
        Load(("Reminders", "The overdue reminder is sent to the assignee."));

        var context = await Search().SearchAsync("and for managers?", "who receives the overdue reminder?");

        Assert.Equal(DocumentContextKind.Found, context.Kind);
        Assert.Equal(["Reminders"], context.Chunks.Select(chunk => chunk.Chunk.HeadingPath));
        Assert.Equal(["and for managers?", "who receives the overdue reminder?\nand for managers?"], _embeddings.Calls.Single().Inputs);
    }

    [Fact]
    public async Task AShortCompleteQuestionIsAnsweredFromItsOwnSearchNotMixedWithThePreviousOne()
    {
        Load(("Reminders", "The overdue reminder is sent to the assignee."), ("Notifications", "Open the mail or bell and click mark as read."));

        var context = await Search().SearchAsync("Can I mark a mail as read?", "Who receives the overdue reminder?");

        Assert.Equal(DocumentContextKind.Found, context.Kind);
        Assert.Equal(["Notifications"], context.Chunks.Select(chunk => chunk.Chunk.HeadingPath));
    }

    [Fact]
    public async Task AShortMessageThatFindsNothingEitherWayIsNoMatch()
    {
        Load(("Reminders", "The overdue reminder is sent to the assignee."));

        var context = await Search().SearchAsync("and then?", "what is the capital of France?");

        Assert.Equal(DocumentContextKind.NoMatch, context.Kind);
    }

    [Fact]
    public async Task ALongMessageIsEmbeddedAlone()
    {
        Load(("Reminders", "The overdue reminder is sent to the assignee."));

        await Search().SearchAsync("Who receives the overdue reminder when a task is late?", "how do I send mail?");

        Assert.Equal(["Who receives the overdue reminder when a task is late?"], _embeddings.Calls.Single().Inputs);
    }

    [Theory]
    [InlineData("and for managers?", "who gets reminders?", "who gets reminders?\nand for managers?")]
    [InlineData("  and for managers?  ", null, null)]
    [InlineData("What validations apply when creating a task in TaskFlow?", "who gets reminders?", null)]
    public void AFollowUpQueryIsOnlyMadeForAShortMessageWithAPreviousOne(string message, string? previous, string? expected)
    {
        Assert.Equal(expected, DocumentSearch.FollowUpQuery(message, previous));
    }
}
