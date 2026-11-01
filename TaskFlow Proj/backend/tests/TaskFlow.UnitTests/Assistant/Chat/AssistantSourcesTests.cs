using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Services.Assistant.Documents;
using TaskFlow.Api.Services.Assistant.Toolbox;

namespace TaskFlow.UnitTests.Assistant.Chat;

/// <summary>Spec requirement "Answers show their sources" (design D10).</summary>
public class AssistantSourcesTests
{
    [Fact]
    public async Task AnAnswerFromDocumentsCarriesEachSectionOnce()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Documents.Result = ScriptedDocumentSearch.Found(
            ("FRD", "4.1.2 Screen: Create Task", "part one"),
            ("FRD", "4.1.2 Screen: Create Task", "part two"),
            ("FRD", "4.1.3 Screen: Task Details", "details"));

        var reply = await h.SendAsync(h.Asha, "what validations apply when creating a task?");

        Assert.Equal(
            [new AssistantSourceDto("FRD", "4.1.2 Screen: Create Task", "FRD.md"), new AssistantSourceDto("FRD", "4.1.3 Screen: Task Details", "FRD.md")],
            reply.Messages.Last().Sources);
        Assert.Empty(reply.Messages.First().Sources);
    }

    [Theory]
    [InlineData(DocumentContextKind.NoMatch)]
    [InlineData(DocumentContextKind.Unavailable)]
    public async Task AnAnswerWithoutDocumentTextHasNoSources(DocumentContextKind kind)
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Documents.Result = kind == DocumentContextKind.NoMatch ? DocumentContext.NoMatch : DocumentContext.Unavailable(DocumentUnavailableReason.CannotSearch);

        var reply = await h.SendAsync(h.Asha, "hello");

        Assert.Empty(reply.Messages.Last().Sources);
    }

    [Fact]
    public async Task AFixedToolReplyHasNoSources()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Documents.Result = ScriptedDocumentSearch.Found(("FRD", "Roles", "Only an Administrator changes roles."));
        h.Gateway.Handler = (_, _) => new ChatModelResult(null, [new ChatToolCall("c1", "echo", "{}")]);
        h.Toolbox.Handler = _ => new ToolOutcome("{}", FixedReply: "Only an administrator can do that.", FixedKind: AssistantMessageKind.Refusal);

        var reply = await h.SendAsync(h.Asha, "make me a manager");

        Assert.Equal(AssistantMessageKind.Refusal, reply.Messages.Last().Kind);
        Assert.Empty(reply.Messages.Last().Sources);
    }

    [Fact]
    public async Task AnEmptyModelAnswerHasNoSources()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Documents.Result = ScriptedDocumentSearch.Found(("FRD", "Roles", "text"));
        h.Gateway.Handler = (_, _) => new ChatModelResult("  ", []);

        var reply = await h.SendAsync(h.Asha, "hello");

        Assert.Empty(reply.Messages.Last().Sources);
    }

    [Fact]
    public async Task SourcesAreShownAgainWhenTheConversationIsReloaded()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Documents.Result = ScriptedDocumentSearch.Found(("Screen 8", "Functional Requirements", "Title is required."));
        await h.SendAsync(h.Asha, "what is required on the create task screen?");

        var conversation = await h.Service.GetConversationAsync(AssistantServiceHarness.Caller(h.Asha));

        Assert.Equal([new AssistantSourceDto("Screen 8", "Functional Requirements", "Screen 8.md")], conversation.Messages.Last().Sources);
    }

    [Fact]
    public async Task AMessageStoredBeforeSourcesExistedLoadsWithNone()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        await h.SendAsync(h.Asha, "hello");
        var stored = await h.Tracking.Db.AssistantMessages.OrderBy(message => message.Id).LastAsync();
        stored.MetaJson = """{"kind":"Text","links":[],"rankedTasks":[],"actionId":null}""";
        await h.Tracking.Db.SaveChangesAsync();

        var conversation = await h.Service.GetConversationAsync(AssistantServiceHarness.Caller(h.Asha));

        Assert.NotNull(conversation.Messages.Last().Sources);
        Assert.Empty(conversation.Messages.Last().Sources);
    }
}
