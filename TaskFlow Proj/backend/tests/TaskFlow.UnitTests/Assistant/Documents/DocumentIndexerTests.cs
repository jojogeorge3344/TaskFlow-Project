using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Integrations.Ai.Embeddings;

namespace TaskFlow.UnitTests.Assistant.Documents;

public class DocumentIndexerTests
{
    private const string TaskDoc = "# Task Guide\n\n## Create a task\nA task needs a title, a deadline and an assignee before it can be saved.";
    private const string MailDoc = "# Mail Guide\n\n## Compose mail\nInternal mail is written in the Compose screen and sent to active users.";

    [Fact]
    public async Task TheFirstRunIndexesEveryMarkdownFileAndIgnoresOtherTypes()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("TF - FRD/02-TaskFlow-Monitoring/FRD-Task-Management.md", TaskDoc);
        h.Write("mail.md", MailDoc);
        h.Write("TF - FRD/02-TaskFlow-Monitoring/FRD-Task-Management.pdf", "%PDF task");
        h.Write("Permission Requirement/01-All-Permissions.xlsx", "xlsx task");

        var run = await h.RunAsync();

        Assert.Equal(2, run.Embedded);
        var stored = await h.StoredChunksAsync();
        Assert.Equal(
            ["TF - FRD/02-TaskFlow-Monitoring/FRD-Task-Management.md", "mail.md"],
            stored.Select(row => row.RelativePath).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(stored, row => row.HeadingPath == "Create a task" && row.Title == "Task Guide");
        Assert.All(h.Embeddings.EmbeddedDocuments, input => Assert.StartsWith("Document: ", input));
        Assert.Equal(2, h.Index.Count);
        var status = h.Status.Current;
        Assert.Equal((2, 2, false, true, null), (status.FileCount, status.ChunkCount, status.IsRunning, status.FolderFound, status.LastError));
        Assert.Equal("gemini-embedding-2@768", status.EmbeddingModel);
        Assert.NotNull(status.LastIndexedAt);
    }

    [Fact]
    public async Task ASecondRunEmbedsNothing()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("task.md", TaskDoc);
        h.Write("mail.md", MailDoc);
        await h.RunAsync();
        h.Embeddings.Calls.Clear();

        var run = await h.RunAsync();

        Assert.Empty(h.Embeddings.Calls);
        Assert.Equal((0, 2, 0), (run.Embedded, run.Skipped, run.Removed));
        Assert.Equal(2, h.Index.Count);
    }

    [Fact]
    public async Task AnEditedFileIsReEmbeddedAloneAndItsNewTextIsSearched()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("task.md", TaskDoc);
        h.Write("mail.md", MailDoc);
        await h.RunAsync();
        h.Embeddings.Calls.Clear();

        h.Write("task.md", TaskDoc.Replace("an assignee", "an assignee and a reminder"));
        var run = await h.RunAsync();

        Assert.Equal(1, run.Embedded);
        Assert.All(h.Embeddings.EmbeddedDocuments, input => Assert.Contains("Task Guide", input));
        var found = h.Index.Search(FakeEmbeddingClient.Vector("task reminder"), 5, 0.5, 6000);
        Assert.Contains(found, result => result.Chunk.Text.Contains("and a reminder"));
        Assert.DoesNotContain((await h.StoredChunksAsync()), row => row.Text.EndsWith("an assignee before it can be saved."));
    }

    [Fact]
    public async Task ADeletedFilesChunksAreRemoved()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("task.md", TaskDoc);
        h.Write("mail.md", MailDoc);
        await h.RunAsync();

        h.Delete("mail.md");
        var run = await h.RunAsync();

        Assert.Equal(1, run.Removed);
        Assert.Equal(["task.md"], (await h.StoredChunksAsync()).Select(row => row.RelativePath).Distinct());
        Assert.DoesNotContain(h.Index.Search(FakeEmbeddingClient.Vector("mail"), 5, 0, 6000), result => result.Chunk.RelativePath == "mail.md");
        Assert.Equal(1, h.Status.Current.FileCount);
    }

    [Fact]
    public async Task AModelChangeReEmbedsEverything()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("task.md", TaskDoc);
        h.Write("mail.md", MailDoc);
        await h.RunAsync();
        h.Embeddings.Calls.Clear();

        h.Options.EmbeddingModel = "gemini-embedding-next";
        var run = await h.RunAsync();

        Assert.Equal(2, run.Embedded);
        Assert.All(h.Embeddings.Calls, call => Assert.Equal("gemini-embedding-next", call.Endpoint.Model));
        Assert.All(await h.StoredChunksAsync(), row => Assert.Equal("gemini-embedding-next@768", row.EmbeddingModel));
        Assert.Equal("gemini-embedding-next@768", h.Index.Model);
    }

    [Fact]
    public async Task AFailingFileDoesNotBlockTheOthers()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("a-broken.md", "# Broken\n\n## Bad\nThis file makes the embedder throw boom.");
        h.Write("task.md", TaskDoc);
        h.Embeddings.FailFor = input => input.Contains("boom") ? new InvalidOperationException("bad input") : null;

        var run = await h.RunAsync();

        Assert.Equal((1, 1), (run.Embedded, run.Failed));
        Assert.Equal(["task.md"], (await h.StoredChunksAsync()).Select(row => row.RelativePath).Distinct());
        Assert.Equal("a-broken.md", h.Status.Current.LastErrorFile);
        Assert.Contains("a-broken.md", h.Status.Current.LastError);
        Assert.False(h.Status.Current.IsRunning);
    }

    [Fact]
    public async Task AnUnreachableEmbeddingServerStopsTheRunAndKeepsThePreviousChunksSearchable()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("task.md", TaskDoc);
        await h.RunAsync();

        h.Write("task.md", TaskDoc + " Edited.");
        h.Write("mail.md", MailDoc);
        h.Embeddings.FailFor = _ => new EmbeddingUnavailableException(EmbeddingFailureKind.Unreachable, "gemini-embedding-2");
        var run = await h.RunAsync();

        Assert.Equal((0, 1), (run.Embedded, run.Failed));
        Assert.Contains("cannot be reached", h.Status.Current.LastError);
        Assert.Equal(["task.md"], (await h.StoredChunksAsync()).Select(row => row.RelativePath).Distinct());
        Assert.Single(h.Index.Search(FakeEmbeddingClient.Vector("task"), 5, 0.5, 6000));
    }

    [Fact]
    public async Task AMissingFolderLeavesAnEmptyIndexAndSaysSo()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("task.md", TaskDoc);
        await h.RunAsync();
        Directory.Delete(h.Folder, recursive: true);

        var run = await h.RunAsync();

        Assert.Equal(1, run.Removed);
        Assert.Equal(0, h.Index.Count);
        Assert.False(h.Status.Current.FolderFound);
        Assert.Contains("documents folder was not found", h.Status.Current.LastError);
        Assert.Contains(h.Folder, h.Status.Current.SourcePath);
    }

    [Fact]
    public async Task TheSavedGeminiKeyModelAndSizeAreUsed()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("task.md", TaskDoc);

        await h.RunAsync();

        var endpoint = h.Embeddings.Calls.Single().Endpoint;
        Assert.Equal(("gemini-embedding-2", 768, DocumentIndexerHarness.GeminiKey), (endpoint.Model, endpoint.Dimensions, endpoint.ApiKey));
    }

    [Fact]
    public async Task ASizeChangeReEmbedsEverything()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("task.md", TaskDoc);
        await h.RunAsync();
        h.Embeddings.Calls.Clear();

        h.Options.EmbeddingDimensions = 1536;
        var run = await h.RunAsync();

        Assert.Equal(1, run.Embedded);
        Assert.Equal(1536, h.Embeddings.Calls.Single().Endpoint.Dimensions);
        Assert.Equal("gemini-embedding-2@1536", h.Index.Model);
    }

    [Fact]
    public async Task AnOldLocalIndexIsReplacedByGeminiEmbeddings()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("task.md", TaskDoc);
        h.Write("mail.md", MailDoc);
        await h.RunAsync();
        await using (var db = h.NewContext())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSources SET EmbeddingModel = 'nomic-embed-text'");
        }

        h.Embeddings.Calls.Clear();
        h.Index.Replace([], "nomic-embed-text");
        var run = await h.RunAsync();

        Assert.Equal(2, run.Embedded);
        Assert.All(await h.StoredChunksAsync(), row => Assert.Equal("gemini-embedding-2@768", row.EmbeddingModel));
        Assert.Equal("gemini-embedding-2@768", h.Index.Model);
        Assert.Equal(2, h.Index.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-decryptable")]
    public async Task WithoutAUsableGeminiKeyNothingIsEmbeddedAndTheReasonIsKept(string? savedKey)
    {
        await using var h = new DocumentIndexerHarness();
        await h.SetSavedGeminiKeyAsync(savedKey);
        h.Write("task.md", TaskDoc);

        var run = await h.RunAsync();

        Assert.Equal((0, 1), (run.Embedded, run.Failed));
        Assert.Empty(h.Embeddings.Calls);
        Assert.Empty(await h.StoredChunksAsync());
        Assert.Equal(0, h.Index.Count);
        Assert.Contains("Google Gemini API key", h.Status.Current.LastError);
    }

    [Fact]
    public async Task AKeySavedLaterIsUsedByTheNextRun()
    {
        await using var h = new DocumentIndexerHarness();
        await h.SetSavedGeminiKeyAsync(null);
        h.Write("task.md", TaskDoc);
        await h.RunAsync();

        await h.SetSavedGeminiKeyAsync(FakeKeyProtector.Encrypted("saved-later"));
        var run = await h.RunAsync();

        Assert.Equal(1, run.Embedded);
        Assert.Equal("saved-later", h.Embeddings.Calls.Single().Endpoint.ApiKey);
    }

    [Fact]
    public async Task ARateLimitStopsTheRunAndTheNextRunContinues()
    {
        await using var h = new DocumentIndexerHarness();
        h.Write("a-task.md", TaskDoc);
        h.Write("b-mail.md", MailDoc);
        h.Embeddings.FailFor = input => input.Contains("Compose") ? new EmbeddingUnavailableException(EmbeddingFailureKind.RateLimited, "gemini-embedding-2") : null;

        var first = await h.RunAsync();

        Assert.Equal((1, 1), (first.Embedded, first.Failed));
        Assert.Contains("rate limit", h.Status.Current.LastError);
        Assert.Equal(["a-task.md"], (await h.StoredChunksAsync()).Select(row => row.RelativePath).Distinct());

        h.Embeddings.FailFor = _ => null;
        h.Embeddings.Calls.Clear();
        var second = await h.RunAsync();

        Assert.Equal((1, 1), (second.Embedded, second.Skipped));
        Assert.Equal(["a-task.md", "b-mail.md"], (await h.StoredChunksAsync()).Select(row => row.RelativePath).Distinct().Order(StringComparer.Ordinal));
    }
}
