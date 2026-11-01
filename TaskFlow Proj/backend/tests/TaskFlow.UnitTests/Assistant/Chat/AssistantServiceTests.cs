using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Services.Assistant.RateLimits;
using TaskFlow.Api.Services.Assistant.Toolbox;

namespace TaskFlow.UnitTests.Assistant.Chat;

public class AssistantServiceTests
{
    private static string Text(AssistantReplyDto reply) => reply.Messages.Last().Content;

    [Fact]
    public async Task AFollowUpQuestionIsSentWithTheEarlierTurns()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Gateway.Handler = (_, request) => ChatModelResult.FromText($"answer {request.Messages.Count}");

        await h.SendAsync(h.Asha, "what is due this week?");
        await h.SendAsync(h.Asha, "which of those is most urgent?");

        var second = h.Gateway.Calls[1].Request.Messages;
        Assert.Equal(["what is due this week?", "answer 1", "which of those is most urgent?"], second.Select(m => m.Content));
        Assert.Equal([AssistantMessageRole.User, AssistantMessageRole.Assistant, AssistantMessageRole.User], second.Select(m => m.Role));
    }

    [Fact]
    public async Task TheConversationSurvivesAProviderSwitch()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();

        await h.SendAsync(h.Asha, "one");
        await h.SendAsync(h.Asha, "two");
        await h.SendAsync(h.Asha, "three");
        var reply = await h.SendAsync(h.Asha, "four", AiApiType.Gemini);

        var (kind, request) = h.Gateway.Calls.Last();
        Assert.Equal(AiApiType.Gemini, kind);
        Assert.Equal(["one", "OK", "two", "OK", "three", "OK", "four"], request.Messages.Select(m => m.Content));
        Assert.Equal("Google Gemini", reply.Messages.Last().Provider);
        Assert.Equal(h.ProviderIds[AiApiType.Gemini], reply.Messages.Last().ProviderId);
        var conversation = await h.Service.GetConversationAsync(AssistantServiceHarness.Caller(h.Asha));
        Assert.Equal(8, conversation.Messages.Count);
        Assert.Equal(["Ollama", "Google Gemini"], conversation.Messages.Where(m => m.Provider is not null).Select(m => m.Provider!).Distinct());
    }

    [Fact]
    public async Task AnotherUsersConversationIsNeverSeenOrUsed()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        await h.SendAsync(h.Asha, "asha secret topic");

        var raviView = await h.Service.GetConversationAsync(AssistantServiceHarness.Caller(h.Ravi));
        await h.SendAsync(h.Ravi, "ravi question");

        Assert.Empty(raviView.Messages);
        Assert.Null(raviView.ConversationId);
        Assert.Equal(["ravi question"], h.Gateway.Calls.Last().Request.Messages.Select(m => m.Content));
        var ashaView = await h.Service.GetConversationAsync(AssistantServiceHarness.Caller(h.Asha));
        Assert.DoesNotContain(ashaView.Messages, m => m.Content.Contains("ravi"));
    }

    [Fact]
    public async Task NewChatStopsEarlierMessagesBeingUsedAsContext()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        await h.SendAsync(h.Asha, "before");

        await h.Service.NewConversationAsync(AssistantServiceHarness.Caller(h.Asha));
        var cleared = await h.Service.GetConversationAsync(AssistantServiceHarness.Caller(h.Asha));
        await h.SendAsync(h.Asha, "after");

        Assert.Empty(cleared.Messages);
        Assert.Equal(["after"], h.Gateway.Calls.Last().Request.Messages.Select(m => m.Content));
    }

    [Fact]
    public async Task OnlyTheLastTwelveMessagesAreSent()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.RateLimiter = new TaskFlow.Api.Services.Assistant.RateLimits.AssistantRateLimiter(new FixedTimeProvider(DateTime.UtcNow));
        for (var i = 1; i <= 10; i++)
        {
            await h.SendAsync(h.Asha, $"message {i}");
        }

        await h.SendAsync(h.Asha, "latest");

        var sent = h.Gateway.Calls.Last().Request.Messages;
        Assert.Equal(AssistantRules.HistoryMessages + 1, sent.Count);
        Assert.Equal("latest", sent.Last().Content);
        Assert.DoesNotContain(sent, m => m.Content == "message 1");
    }

    [Fact]
    public async Task HistoryIsTrimmedToTheCharacterBudgetFromTheOldestEnd()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        var big = new string('x', 1500);
        h.Gateway.Handler = (_, _) => ChatModelResult.FromText(big);
        for (var i = 0; i < 4; i++)
        {
            await h.SendAsync(h.Asha, $"q{i}");
        }

        await h.SendAsync(h.Asha, "latest");

        var history = h.Gateway.Calls.Last().Request.Messages.SkipLast(1).Sum(m => m.Content.Length);
        Assert.True(history <= AssistantRules.HistoryCharacters, $"history was {history} characters");
        Assert.Equal("latest", h.Gateway.Calls.Last().Request.Messages.Last().Content);
    }

    [Fact]
    public async Task AProviderFailureStoresNothingAndOffersTheOtherProviders()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Gateway.Handler = (kind, _) => throw new ProviderFailureException(kind, ProviderFailureKind.RateLimited, TimeSpan.FromSeconds(30));

        var reply = await h.SendAsync(h.Asha, "hello", AiApiType.Gemini);

        Assert.Empty(reply.Messages);
        var failure = Assert.IsType<AssistantFailureDto>(reply.Failure);
        Assert.Equal(ProviderFailureKind.RateLimited, failure.Kind);
        Assert.Equal(h.ProviderIds[AiApiType.Gemini], failure.ProviderId);
        Assert.Equal("Google Gemini", failure.ProviderName);
        Assert.Equal(30, failure.RetryAfterSeconds);
        Assert.Equal([h.ProviderIds[AiApiType.Ollama]], failure.Alternatives.Select(a => a.ProviderId));
        Assert.Contains("Google Gemini", failure.Message);
        Assert.Single(h.Gateway.Calls);
        Assert.Equal(0, await h.Tracking.Db.AssistantMessages.CountAsync());
    }

    [Fact]
    public async Task AChatFailureNeverShowsTheProvidersOwnReason()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Gateway.Handler = (kind, _) => throw new ProviderFailureException(
            kind, ProviderFailureKind.Unexpected, httpStatus: 400, providerReason: "FAILED_PRECONDITION: User location is not supported");

        var reply = await h.SendAsync(h.Asha, "hello", AiApiType.Gemini);

        var failure = Assert.IsType<AssistantFailureDto>(reply.Failure);
        Assert.Contains("unexpected error", failure.Message);
        Assert.DoesNotContain("location", failure.Message);
        Assert.DoesNotContain("HTTP", failure.Message);
    }

    [Fact]
    public async Task AMalformedModelNameInChatShowsOnlyTheFixedMessage()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Gateway.Handler = (kind, _) => throw new ProviderFailureException(
            kind, ProviderFailureKind.InvalidModelName, httpStatus: 400, providerReason: "INVALID_ARGUMENT: * GenerateContentRequest.model: unexpected model name format");

        var reply = await h.SendAsync(h.Asha, "hello", AiApiType.Gemini);

        var failure = Assert.IsType<AssistantFailureDto>(reply.Failure);
        Assert.Equal(ProviderFailureKind.InvalidModelName, failure.Kind);
        Assert.Contains("not accept the configured model name as a model ID", failure.Message);
        Assert.DoesNotContain("INVALID_ARGUMENT", failure.Message);
    }

    [Fact]
    public async Task ARetryAfterAFailureDoesNotDuplicateTheMessage()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Gateway.Handler = (kind, _) => throw new ProviderFailureException(kind, ProviderFailureKind.Timeout);
        await h.SendAsync(h.Asha, "hello");
        h.Gateway.Handler = (_, _) => ChatModelResult.FromText("hi");

        await h.SendAsync(h.Asha, "hello");

        Assert.Equal(["hello"], h.Gateway.Calls.Last().Request.Messages.Select(m => m.Content));
        Assert.Equal(2, await h.Tracking.Db.AssistantMessages.CountAsync());
    }

    [Fact]
    public async Task AMessageWithASecretIsNeitherSentNorStored()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();

        var reply = await h.SendAsync(h.Asha, "my password is Hunter2!, change it to NewPass#9");

        var notice = Assert.Single(reply.Messages);
        Assert.Equal(AssistantMessageKind.Notice, notice.Kind);
        Assert.Contains("My Profile", notice.Content);
        Assert.Empty(h.Gateway.Calls);
        Assert.Equal(0, await h.Tracking.Db.AssistantMessages.CountAsync());
        Assert.Equal(0, await h.Tracking.Db.AssistantConversations.CountAsync());
    }

    [Fact]
    public async Task AProviderThatIsNotConfiguredIsRejectedWithoutCallingAnyProvider()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();

        var error = await Assert.ThrowsAsync<AssistantRequestException>(() => h.SendAsync(h.Asha, "hi", AiApiType.OpenAI));

        Assert.Contains("OpenAI GPT is not configured", error.Message);
        Assert.Empty(h.Gateway.Calls);
    }

    [Fact]
    public async Task WithNoProviderConfiguredTfaSaysItIsNotAvailable()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        await h.Config.SaveAsync(1, new UpdateAiProvidersRequest(
        [
            new UpdateAiProviderRequest(h.ProviderIds[AiApiType.Ollama], false, "llama3.1:8b", null, null, null),
            new UpdateAiProviderRequest(h.ProviderIds[AiApiType.Gemini], false, "gemini-2.0-flash", null, null, null)
        ]));

        var error = await Assert.ThrowsAsync<AssistantRequestException>(
            () => h.Service.SendAsync(AssistantServiceHarness.Caller(h.Asha), new SendAssistantMessageRequest("hi", null)));

        Assert.Contains("not available yet", error.Message);
        Assert.Empty(h.Gateway.Calls);
    }

    [Fact]
    public async Task WithoutAChosenProviderTheFirstAvailableOneIsUsed()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();

        await h.Service.SendAsync(AssistantServiceHarness.Caller(h.Asha), new SendAssistantMessageRequest("hi", null));

        // Google Gemini is listed first, so it is the default when the user has not chosen one.
        Assert.Equal(AiApiType.Gemini, h.Gateway.Calls.Single().Kind);
    }

    [Fact]
    public async Task TheTwentyFirstMessageInAMinuteIsRefusedBeforeAnyProviderIsCalled()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        for (var i = 0; i < TaskFlow.Api.Services.Assistant.RateLimits.AssistantRateLimiter.RequestsPerMinute; i++)
        {
            await h.SendAsync(h.Asha, $"m{i}");
        }

        var error = await Assert.ThrowsAsync<AssistantRateLimitedException>(() => h.SendAsync(h.Asha, "one too many"));

        Assert.True(error.RetryAfter > TimeSpan.Zero);
        Assert.Equal(TaskFlow.Api.Services.Assistant.RateLimits.AssistantRateLimiter.RequestsPerMinute, h.Gateway.Calls.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyMessageIsRejected(string text)
    {
        await using var h = await AssistantServiceHarness.CreateAsync();

        await Assert.ThrowsAsync<AssistantRequestException>(() => h.SendAsync(h.Asha, text));

        Assert.Empty(h.Gateway.Calls);
    }

    [Fact]
    public async Task AnOversizedMessageIsRejected()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();

        await Assert.ThrowsAsync<AssistantRequestException>(
            () => h.SendAsync(h.Asha, new string('a', AssistantRules.MaxMessageLength + 1)));

        Assert.Empty(h.Gateway.Calls);
    }

    [Fact]
    public async Task ToolCallsRunAndTheirResultsAreGivenBackToTheModel()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Gateway.Handler = (_, request) => request.Messages.Any(m => m.Role == AssistantMessageRole.Tool)
            ? ChatModelResult.FromText("final answer")
            : new ChatModelResult(null, [new ChatToolCall("c1", "echo", "{}")]);

        var reply = await h.SendAsync(h.Asha, "use a tool");

        Assert.Equal("final answer", Text(reply));
        Assert.Equal(2, h.Gateway.Calls.Count);
        var toolMessage = h.Gateway.Calls[1].Request.Messages.Last();
        Assert.Equal(AssistantMessageRole.Tool, toolMessage.Role);
        Assert.Equal("result of echo", toolMessage.Content);
        Assert.Equal("c1", toolMessage.ToolCallId);
    }

    [Fact]
    public async Task ToolResultsAreRedactedBeforeTheModelSeesThem()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Toolbox.Handler = _ => new ToolOutcome("the key is sk-proj-abcdefghijklmnopqrstuv");
        h.Gateway.Handler = (_, request) => request.Messages.Any(m => m.Role == AssistantMessageRole.Tool)
            ? ChatModelResult.FromText("done")
            : new ChatModelResult(null, [new ChatToolCall("c1", "echo", "{}")]);

        await h.SendAsync(h.Asha, "go");

        Assert.DoesNotContain("abcdefghijklmnop", h.Gateway.Calls[1].Request.Messages.Last().Content);
    }

    [Fact]
    public async Task AFixedReplyEndsTheTurnWithoutAskingTheModelToWordIt()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Toolbox.Handler = _ => new ToolOutcome("denied", "You do not have the required permission.", AssistantMessageKind.Refusal);
        h.Gateway.Handler = (_, _) => new ChatModelResult("I will do it anyway!", [new ChatToolCall("c1", "echo", "{}")]);

        var reply = await h.SendAsync(h.Asha, "make me admin");

        var message = reply.Messages.Last();
        Assert.Equal("You do not have the required permission.", message.Content);
        Assert.Equal(AssistantMessageKind.Refusal, message.Kind);
        Assert.Single(h.Gateway.Calls);
    }

    [Fact]
    public async Task TheToolLoopIsCappedAndTheFinalCallOffersNoTools()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Gateway.Handler = (_, request) => request.Tools.Count == 0
            ? ChatModelResult.FromText("gave up looping")
            : new ChatModelResult(null, [new ChatToolCall(Guid.NewGuid().ToString("N")[..8], "echo", "{}")]);

        var reply = await h.SendAsync(h.Asha, "loop forever");

        Assert.Equal("gave up looping", Text(reply));
        Assert.Equal(AssistantRules.MaxToolRounds + 1, h.Gateway.Calls.Count);
        Assert.Empty(h.Gateway.Calls.Last().Request.Tools);
        Assert.Equal(AssistantRules.MaxToolRounds, h.Toolbox.Executed.Count);
    }

    [Fact]
    public async Task LinksAndRankedTasksFromToolsComeBackOnTheMessageAndSurviveAReload()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        var ranked = new PrioritisedTask(1, 7, "TF-7", "Fix bug", TaskPriority.High, TaskFlow.Api.Models.Enums.TaskStatus.InProgress,
            new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 10, 20), 2, TaskFlow.Api.Common.Rules.TaskStage.Upcoming, false);
        h.Toolbox.Handler = _ => new ToolOutcome("ranked", Links: [new AssistantLinkDto("TF-7", "/tasks/7")], RankedTasks: [ranked]);
        h.Gateway.Handler = (_, request) => request.Messages.Any(m => m.Role == AssistantMessageRole.Tool)
            ? ChatModelResult.FromText("Start with TF-7.")
            : new ChatModelResult(null, [new ChatToolCall("c1", "echo", "{}")]);

        var reply = await h.SendAsync(h.Asha, "what first?");
        var reloaded = (await h.Service.GetConversationAsync(AssistantServiceHarness.Caller(h.Asha))).Messages.Last();

        Assert.Equal("/tasks/7", Assert.Single(reply.Messages.Last().Links).Route);
        Assert.Equal("TF-7", Assert.Single(reply.Messages.Last().RankedTasks).TaskCode);
        Assert.Equal("/tasks/7", Assert.Single(reloaded.Links).Route);
        Assert.Equal(7, Assert.Single(reloaded.RankedTasks).TaskId);
    }

    [Fact]
    public async Task NoticeMessagesAreNotUsedAsContext()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        h.Toolbox.Handler = _ => new ToolOutcome("x", "Fixed notice text.", AssistantMessageKind.Notice);
        h.Gateway.Handler = (_, _) => new ChatModelResult(null, [new ChatToolCall("c1", "echo", "{}")]);
        await h.SendAsync(h.Asha, "first");
        h.Gateway.Handler = (_, _) => ChatModelResult.FromText("ok");

        await h.SendAsync(h.Asha, "second");

        Assert.Equal(["first", "second"], h.Gateway.Calls.Last().Request.Messages.Select(m => m.Content));
    }

    [Fact]
    public async Task TheConversationEndpointReturnsOnlyTheLatestMessagesInOrder()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();
        var clock = new FixedTimeProvider(DateTime.UtcNow);
        h.RateLimiter = new TaskFlow.Api.Services.Assistant.RateLimits.AssistantRateLimiter(clock);
        for (var i = 1; i <= 60; i++)
        {
            await h.SendAsync(h.Asha, $"question {i}");
            clock.Set(clock.GetUtcNow().UtcDateTime.AddSeconds(10));
        }

        var conversation = await h.Service.GetConversationAsync(AssistantServiceHarness.Caller(h.Asha));

        Assert.Equal(AssistantRules.ConversationMessages, conversation.Messages.Count);
        Assert.Equal("OK", conversation.Messages.Last().Content);
        Assert.Equal("question 60", conversation.Messages[^2].Content);
        Assert.Equal("question 11", conversation.Messages.First().Content);
        Assert.Equal(conversation.Messages.Select(m => m.Id).Order(), conversation.Messages.Select(m => m.Id));
        Assert.Equal(120, await h.Tracking.Db.AssistantMessages.CountAsync());
    }

    [Fact]
    public async Task TheSystemPromptNamesTheUserTheirRoleAndTodayAndTheDataRule()
    {
        await using var h = await AssistantServiceHarness.CreateAsync();

        await h.SendAsync(h.Asha, "hello");

        var prompt = h.Gateway.Calls.Single().Request.SystemPrompt;
        Assert.Contains("asha full name", prompt);
        Assert.Contains("Employee", prompt);
        Assert.Contains("is data", prompt);
        Assert.Contains("Never ask for, accept or repeat passwords", prompt);
        Assert.Contains("only change the signed-in user", prompt);
        Assert.Contains("needs an administrator", prompt);
    }
}
