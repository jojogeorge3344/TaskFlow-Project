using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Api.Config;
using TaskFlow.Api.Integrations.Ai.Embeddings;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Assistant.Documents;
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
    private readonly DocumentKnowledgeOptions _options = new() { MinScore = 0.5, KeywordAssistMinScore = 0.4 };
    private readonly TaskFlow.UnitTests.TestSupport.CapturingLogger<DocumentSearch> _logger = new();

    public DocumentSearchTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        // Migrations, not EnsureCreated: the keyword index (DocumentChunks_fts) and its triggers come from a migration.
        _db.Database.Migrate();
        _db.AiProviderConfigs.Add(new TaskFlow.Api.Models.Entities.AiProviderConfig
        {
            Name = "Google Gemini", Locality = "Cloud", IsBuiltIn = true, ApiType = TaskFlow.Api.Models.Enums.AiApiType.Gemini,
            ModelName = "gemini-3.8-flash", EncryptedApiKey = FakeKeyProtector.Encrypted("test-gemini-key"), UpdatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
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
        return new DocumentSearch(_index, _status, _embeddings, new DocumentEmbeddingEndpoint(new AiProviderConfigRepository(_db), new FakeKeyProtector(), options), new DocumentIndexRepository(_db), options, _logger);
    }

    private void Load(params (string Heading, string Text)[] chunks) =>
        _index.Replace(
            chunks.Select((chunk, index) => new IndexedChunk(index + 1, "FRD.md", "FRD", chunk.Heading, chunk.Text, FakeEmbeddingClient.Vector(chunk.Text))).ToList(),
            _options.IndexModel);

    [Fact]
    public async Task MatchingChunksAreFound()
    {
        Load(("Reminders", "The overdue reminder is sent to the assignee."), ("Mail", "Compose mail screen."));

        var context = await Search().SearchAsync("who receives the overdue reminder?", null);

        Assert.Equal(DocumentContextKind.Found, context.Kind);
        Assert.Equal(["Reminders"], context.Chunks.Select(chunk => chunk.Chunk.HeadingPath));
        var call = Assert.Single(_embeddings.Calls);
        Assert.Equal(EmbeddingPurpose.Query, call.Purpose);
        Assert.Equal(("gemini-embedding-2", 768, "test-gemini-key"), (call.Endpoint.Model, call.Endpoint.Dimensions, call.Endpoint.ApiKey));
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
        _embeddings.FailFor = _ => new EmbeddingUnavailableException(EmbeddingFailureKind.Unreachable, "gemini-embedding-2");

        var context = await Search().SearchAsync("how do I send mail?", null);

        Assert.Equal((DocumentContextKind.Unavailable, DocumentUnavailableReason.CannotSearch), (context.Kind, context.Reason));
    }

    [Fact]
    public async Task NoSavedGeminiKeyIsUnavailableAndNothingIsSent()
    {
        Load(("Mail", "Compose mail screen."));
        var gemini = await _db.AiProviderConfigs.SingleAsync();
        gemini.EncryptedApiKey = null;
        await _db.SaveChangesAsync();

        var context = await Search().SearchAsync("how do I send mail?", null);

        Assert.Equal((DocumentContextKind.Unavailable, DocumentUnavailableReason.CannotSearch), (context.Kind, context.Reason));
        Assert.Empty(_embeddings.Calls);
    }

    [Fact]
    public async Task AGeminiRateLimitIsUnavailable()
    {
        Load(("Mail", "Compose mail screen."));
        _embeddings.FailFor = _ => new EmbeddingUnavailableException(EmbeddingFailureKind.RateLimited, "gemini-embedding-2");

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
        _options.EmbeddingModel = "gemini-embedding-next";

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

    // ------------------------------------------------------------------ hybrid search (tfa-hybrid-document-search)

    /// <summary>Stores the chunks in the database (so the keyword index has them) and loads them into the in-memory index
    /// with a vector whose similarity to <paramref name="query"/>'s fake embedding is exactly the given score.</summary>
    private void Store(string query, params (string Heading, string Text, double Similarity)[] chunks)
    {
        var source = new TaskFlow.Api.Models.Entities.DocumentSource
        {
            RelativePath = "FRD.md", Title = "FRD", ContentHash = new string('0', 64), EmbeddingModel = _options.IndexModel,
            ChunkCount = chunks.Length, IndexedAt = DateTime.UtcNow,
            Chunks = chunks.Select((chunk, index) => new TaskFlow.Api.Models.Entities.DocumentChunk
            {
                Ordinal = index, HeadingPath = chunk.Heading, Text = chunk.Text, Embedding = [0, 0, 128, 63], Dimensions = 1
            }).ToList()
        };
        _db.DocumentSources.Add(source);
        _db.SaveChanges();

        var target = FakeEmbeddingClient.Vector(query);
        _index.Replace(
            source.Chunks.Select((chunk, index) => new IndexedChunk(
                chunk.Id, "FRD.md", "FRD", chunk.HeadingPath, chunk.Text, WithSimilarity(target, chunks[index].Similarity))).ToList(),
            _options.IndexModel);
    }

    /// <summary>A unit vector whose dot product with the unit vector <paramref name="target"/> is <paramref name="similarity"/>.</summary>
    private static float[] WithSimilarity(float[] target, double similarity)
    {
        var axis = Enumerable.Range(0, target.Length).MinBy(index => Math.Abs(target[index]));
        var other = target.Select((value, index) => (index == axis ? 1.0 : 0.0) - target[axis] * value).ToArray();
        var length = Math.Sqrt(other.Sum(value => value * value));
        var sideways = Math.Sqrt(1 - similarity * similarity);
        return target.Select((value, index) => (float)(similarity * value + sideways * other[index] / length)).ToArray();
    }

    private void UseProductionThresholds()
    {
        _options.MinScore = 0.65;
        _options.KeywordAssistMinScore = 0.55;
    }

    [Fact]
    public async Task AnExactIdentifierIsFoundByKeywordBelowTheMinimumSimilarity()
    {
        const string question = "what does NFR-14 say?";
        UseProductionThresholds();
        Store(question, ("Non-functional requirements", "NFR-14 The reminder engine runs every 60 seconds.", 0.2), ("Mail", "Compose mail screen.", 0.3));

        var context = await Search().SearchAsync(question, null);

        Assert.Equal(DocumentContextKind.Found, context.Kind);
        Assert.Equal(["Non-functional requirements"], context.Chunks.Select(chunk => chunk.Chunk.HeadingPath));
    }

    [Fact]
    public async Task AKeywordMatchLiftsABorderlineChunk()
    {
        const string question = "overdue escalation rules";
        UseProductionThresholds();
        Store(question, ("Escalation", "Overdue escalation rules for managers.", 0.6), ("Other", "Something similar but unrelated.", 0.6));

        var context = await Search().SearchAsync(question, null);

        Assert.Equal(["Escalation"], context.Chunks.Select(chunk => chunk.Chunk.HeadingPath));
    }

    [Fact]
    public async Task AnOrdinaryWordMatchBelowTheAssistMinimumIsNotUsed()
    {
        const string question = "overdue escalation rules";
        UseProductionThresholds();
        Store(question, ("Escalation", "Overdue escalation rules for managers.", 0.4));

        var context = await Search().SearchAsync(question, null);

        Assert.Equal(DocumentContextKind.NoMatch, context.Kind);
    }

    [Fact]
    public async Task ASimilarChunkWithoutAKeywordMatchIsStillFound()
    {
        const string question = "who receives the overdue reminder?";
        UseProductionThresholds();
        Store(question, ("Reminders", "The assignee is notified when work is late.", 0.8));

        Assert.Equal(["Reminders"], (await Search().SearchAsync(question, null)).Chunks.Select(chunk => chunk.Chunk.HeadingPath));
    }

    [Fact]
    public async Task SearchOperatorsInTheMessageAreReadAsPlainWords()
    {
        const string question = "what is \"AND\" OR NOT NEAR(task) title:x*?";
        Store(question, ("Tasks", "A task has a title.", 0.1));

        var context = await Search().SearchAsync(question, null);

        Assert.Equal(DocumentContextKind.NoMatch, context.Kind);
    }

    [Fact]
    public async Task WithHybridSearchOffOnlySimilarityCounts()
    {
        const string question = "what does NFR-14 say?";
        UseProductionThresholds();
        _options.HybridSearch = false;
        Store(question, ("Non-functional requirements", "NFR-14 The reminder engine runs every 60 seconds.", 0.2), ("Close", "Close match.", 0.7));

        var context = await Search().SearchAsync(question, null);

        Assert.Equal(["Close"], context.Chunks.Select(chunk => chunk.Chunk.HeadingPath));
    }

    [Fact]
    public async Task WithHybridSearchOffAFailedEmbeddingIsUnavailable()
    {
        const string question = "what does NFR-14 say?";
        _options.HybridSearch = false;
        Store(question, ("Non-functional requirements", "NFR-14 The reminder engine runs every 60 seconds.", 0.2));
        _embeddings.FailFor = _ => new EmbeddingUnavailableException(EmbeddingFailureKind.RateLimited, "gemini-embedding-2");

        var context = await Search().SearchAsync(question, null);

        Assert.Equal((DocumentContextKind.Unavailable, DocumentUnavailableReason.CannotSearch), (context.Kind, context.Reason));
    }

    [Fact]
    public async Task WithoutAGeminiKeyAKeywordMatchIsStillFoundAndTheKeyIsAskedFor()
    {
        const string question = "what does NFR-14 say?";
        Store(question, ("Non-functional requirements", "NFR-14 The reminder engine runs every 60 seconds.", 0.2));
        var gemini = await _db.AiProviderConfigs.SingleAsync();
        gemini.EncryptedApiKey = null;
        await _db.SaveChangesAsync();

        var context = await Search().SearchAsync(question, null);

        Assert.Equal(DocumentContextKind.Found, context.Kind);
        Assert.Equal(["Non-functional requirements"], context.Chunks.Select(chunk => chunk.Chunk.HeadingPath));
        Assert.Empty(_embeddings.Calls);
        Assert.Contains(_logger.Messages, message => message.Contains("Google Gemini API key"));
    }

    [Fact]
    public async Task WithoutAGeminiKeyAndNoKeywordMatchTheDocumentsCannotBeSearched()
    {
        const string question = "how are payroll exports approved?";
        Store(question, ("Mail", "Compose mail screen.", 0.9));
        var gemini = await _db.AiProviderConfigs.SingleAsync();
        gemini.EncryptedApiKey = null;
        await _db.SaveChangesAsync();

        var context = await Search().SearchAsync(question, null);

        Assert.Equal((DocumentContextKind.Unavailable, DocumentUnavailableReason.CannotSearch), (context.Kind, context.Reason));
        Assert.Contains(_logger.Messages, message => message.Contains("Google Gemini API key"));
    }

    [Fact]
    public async Task AtTheGeminiRateLimitAKeywordMatchOfOrdinaryWordsIsUsed()
    {
        const string question = "overdue escalation rules";
        Store(question, ("Escalation", "Overdue escalation rules for managers.", 0.1));
        _embeddings.FailFor = _ => new EmbeddingUnavailableException(EmbeddingFailureKind.RateLimited, "gemini-embedding-2");

        var context = await Search().SearchAsync(question, null);

        Assert.Equal(["Escalation"], context.Chunks.Select(chunk => chunk.Chunk.HeadingPath));
    }

    [Fact]
    public async Task WithoutEmbeddingsAShortFollowUpIsSearchedWithThePreviousQuestion()
    {
        Store("x", ("Escalation", "Overdue escalation rules for managers.", 0.1));
        _embeddings.FailFor = _ => new EmbeddingUnavailableException(EmbeddingFailureKind.Unreachable, "gemini-embedding-2");

        var context = await Search().SearchAsync("and managers?", "overdue escalation rules");

        Assert.Equal(["Escalation"], context.Chunks.Select(chunk => chunk.Chunk.HeadingPath));
    }
}
