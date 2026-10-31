using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Services.Settings.TfaSettings;
using TaskFlow.Api.Integrations.Ai;
using TaskFlow.Api.Services.Settings.ApplicationSettings;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Repositories.Assistant;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.Api.Services.Assistant;

/// <summary>TFA's conversation engine. A message goes through, in order: the length and secret checks, the per-minute
/// limit, then the chosen provider with a small tool loop. Nothing is stored until the provider has answered, so a
/// failed call leaves the conversation exactly as it was and the user can simply retry.</summary>
public class AssistantService : IAssistantService
{
    internal static readonly JsonSerializerOptions MetaJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IAssistantConversationRepository _conversations;
    private readonly IAssistantMessageRepository _messages;
    private readonly IAssistantDirectoryRepository _directory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatModelGateway _gateway;
    private readonly IAiProviderConfigService _providers;
    private readonly IAssistantRateLimiter _rateLimiter;
    private readonly IAssistantToolbox _toolbox;
    private readonly IAssistantActionCards _actionCards;
    private readonly IApplicationSettingsService _applicationSettings;
    private readonly IAppClock _clock;
    private readonly TimeProvider _timeProvider;

    public AssistantService(
        IAssistantConversationRepository conversations,
        IAssistantMessageRepository messages,
        IAssistantDirectoryRepository directory,
        IUnitOfWork unitOfWork,
        IChatModelGateway gateway,
        IAiProviderConfigService providers,
        IAssistantRateLimiter rateLimiter,
        IAssistantToolbox toolbox,
        IAssistantActionCards actionCards,
        IApplicationSettingsService applicationSettings,
        IAppClock clock,
        TimeProvider timeProvider)
    {
        _conversations = conversations;
        _messages = messages;
        _directory = directory;
        _unitOfWork = unitOfWork;
        _gateway = gateway;
        _providers = providers;
        _rateLimiter = rateLimiter;
        _toolbox = toolbox;
        _actionCards = actionCards;
        _applicationSettings = applicationSettings;
        _clock = clock;
        _timeProvider = timeProvider;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<AssistantConversationDto> GetConversationAsync(AssistantCaller caller, CancellationToken cancellationToken = default)
    {
        var conversation = await ActiveConversationAsync(caller.UserId, cancellationToken);
        if (conversation is null)
        {
            return new AssistantConversationDto(null, []);
        }

        var messages = (await _messages.GetNewestAsync(conversation.Id, AssistantRules.ConversationMessages, cancellationToken)).Reverse().ToList();

        return new AssistantConversationDto(conversation.Id, await ToDtosAsync(caller.UserId, messages, cancellationToken));
    }

    public async Task NewConversationAsync(AssistantCaller caller, CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        _conversations.Add(new AssistantConversation { UserId = caller.UserId, CreatedAt = now, UpdatedAt = now });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AssistantReplyDto> SendAsync(AssistantCaller caller, SendAssistantMessageRequest request, CancellationToken cancellationToken = default)
    {
        var text = request.Text?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Length > AssistantRules.MaxMessageLength)
        {
            throw new AssistantRequestException($"A message must be between 1 and {AssistantRules.MaxMessageLength} characters.");
        }

        // A message with a password or key in it is neither sent nor stored.
        if (SecretScreen.ContainsSecret(text))
        {
            return new AssistantReplyDto([Transient(AssistantTexts.SecretBlocked, AssistantMessageKind.Notice)], null);
        }

        var available = await _providers.GetAvailableAsync(cancellationToken);
        var provider = request.Provider ?? available.FirstOrDefault()?.Kind;
        if (provider is not { } kind || available.All(option => option.Kind != kind))
        {
            throw new AssistantRequestException(
                provider is { } missing
                    ? ProviderFailureMessages.NotConfigured(missing)
                    : "TFA is not available yet. An administrator needs to configure an AI provider.");
        }

        if (_rateLimiter.TryAcquire(caller.UserId) is { } wait)
        {
            throw new AssistantRateLimitedException(wait);
        }

        var conversation = await ActiveConversationAsync(caller.UserId, cancellationToken)
            ?? await CreateConversationAsync(caller.UserId, cancellationToken);

        var history = await LoadHistoryAsync(conversation.Id, cancellationToken);
        var turn = new List<ChatMessage>(history) { new(AssistantMessageRole.User, text) };

        var userName = await _directory.GetFullNameAsync(caller.UserId, cancellationToken) ?? "the user";
        var systemPrompt = AssistantPrompt.Build(
            userName,
            caller.Role,
            await _clock.TodayLocalAsync(cancellationToken),
            (await _applicationSettings.GetAsync(cancellationToken)).Name);

        Answer answer;
        try
        {
            answer = await RunTurnAsync(caller, conversation.Id, kind, systemPrompt, turn, cancellationToken);
        }
        catch (ProviderFailureException failure)
        {
            var alternatives = available.Where(option => option.Kind != failure.Provider).ToList();
            return new AssistantReplyDto(
                [],
                new AssistantFailureDto(
                    failure.Kind,
                    failure.Provider,
                    ProviderFailureMessages.For(failure.Provider, failure.Kind, failure.RetryAfter),
                    failure.RetryAfter is { } retry ? (int)Math.Ceiling(retry.TotalSeconds) : null,
                    alternatives));
        }

        var now = UtcNow;
        var userMessage = new AssistantMessage
        {
            ConversationId = conversation.Id,
            Role = AssistantMessageRole.User,
            Content = text,
            CreatedAt = now
        };
        var assistantMessage = new AssistantMessage
        {
            ConversationId = conversation.Id,
            Role = AssistantMessageRole.Assistant,
            Content = answer.Text,
            ProviderKind = kind,
            MetaJson = JsonSerializer.Serialize(
                new AssistantMessageMeta(answer.Kind, answer.Links, answer.RankedTasks, answer.ActionId),
                MetaJson),
            CreatedAt = now.AddMilliseconds(1)
        };
        _messages.Add(userMessage);
        _messages.Add(assistantMessage);
        conversation.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssistantReplyDto(await ToDtosAsync(caller.UserId, [userMessage, assistantMessage], cancellationToken), null);
    }

    // ------------------------------------------------------------------------------------------- the model loop

    private sealed record Answer(
        string Text,
        AssistantMessageKind Kind,
        IReadOnlyList<AssistantLinkDto> Links,
        IReadOnlyList<PrioritisedTask> RankedTasks,
        Guid? ActionId);

    private async Task<Answer> RunTurnAsync(
        AssistantCaller caller,
        int conversationId,
        AiProviderKind kind,
        string systemPrompt,
        List<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var tools = await _toolbox.DefinitionsForAsync(caller, cancellationToken);
        var links = new List<AssistantLinkDto>();
        var ranked = new List<PrioritisedTask>();
        Guid? actionId = null;

        for (var round = 0; round <= AssistantRules.MaxToolRounds; round++)
        {
            // After the last allowed round the model is asked once more with no tools, so it must answer in words.
            var offered = round == AssistantRules.MaxToolRounds ? [] : tools;
            var result = await _gateway.CompleteAsync(kind, new ChatModelRequest(systemPrompt, messages, offered, string.Empty), cancellationToken);

            if (result.ToolCalls.Count == 0)
            {
                var text = string.IsNullOrWhiteSpace(result.Text) ? AssistantTexts.EmptyAnswer : result.Text.Trim();
                return new Answer(text, AssistantMessageKind.Text, Distinct(links), ranked, actionId);
            }

            messages.Add(new ChatMessage(AssistantMessageRole.Assistant, result.Text ?? string.Empty, result.ToolCalls));

            foreach (var call in result.ToolCalls)
            {
                var outcome = await _toolbox.ExecuteAsync(caller, conversationId, call, cancellationToken);
                links.AddRange(outcome.Links ?? []);
                ranked.AddRange(outcome.RankedTasks ?? []);
                actionId ??= outcome.PendingActionId;

                if (outcome.FixedReply is { } fixedReply)
                {
                    // A refusal or a prepared action ends the turn with exact text. The model is not asked to word it.
                    return new Answer(fixedReply, outcome.FixedKind, Distinct(links), ranked, actionId);
                }

                messages.Add(new ChatMessage(
                    AssistantMessageRole.Tool,
                    SecretScreen.Redact(outcome.ResultForModel),
                    ToolCallId: call.Id,
                    ToolName: call.Name));
            }
        }

        return new Answer(AssistantTexts.EmptyAnswer, AssistantMessageKind.Text, Distinct(links), ranked, actionId);
    }

    private static IReadOnlyList<AssistantLinkDto> Distinct(IEnumerable<AssistantLinkDto> links) =>
        links.GroupBy(link => link.Route).Select(group => group.First()).ToList();

    // ------------------------------------------------------------------------------------------- storage

    private Task<AssistantConversation?> ActiveConversationAsync(int userId, CancellationToken cancellationToken) =>
        _conversations.FindLatestForUpdateAsync(userId, cancellationToken);

    private async Task<AssistantConversation> CreateConversationAsync(int userId, CancellationToken cancellationToken)
    {
        var now = UtcNow;
        var conversation = new AssistantConversation { UserId = userId, CreatedAt = now, UpdatedAt = now };
        _conversations.Add(conversation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    /// <summary>The recent conversation as neutral text turns: the last 12 messages, trimmed from the oldest end until
    /// they fit the character budget. Notices are left out because they are not part of what was said.</summary>
    private async Task<List<ChatMessage>> LoadHistoryAsync(int conversationId, CancellationToken cancellationToken)
    {
        var stored = await _messages.GetNewestAsync(conversationId, AssistantRules.HistoryMessages * 2, cancellationToken);

        var turns = new List<ChatMessage>();
        var characters = 0;
        foreach (var message in stored.Where(message => !IsNotice(message)).Take(AssistantRules.HistoryMessages))
        {
            if (characters + message.Content.Length > AssistantRules.HistoryCharacters && turns.Count > 0)
            {
                break;
            }

            characters += message.Content.Length;
            turns.Add(new ChatMessage(message.Role, message.Content));
        }

        turns.Reverse();
        return turns;
    }

    private static bool IsNotice(AssistantMessage message) => ReadMeta(message)?.Kind == AssistantMessageKind.Notice;

    private static AssistantMessageMeta? ReadMeta(AssistantMessage message)
    {
        if (string.IsNullOrEmpty(message.MetaJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AssistantMessageMeta>(message.MetaJson, MetaJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<AssistantMessageDto>> ToDtosAsync(
        int userId,
        IReadOnlyList<AssistantMessage> messages,
        CancellationToken cancellationToken)
    {
        var metas = messages.ToDictionary(message => message, ReadMeta);
        var actionIds = metas.Values.Where(meta => meta?.ActionId is not null).Select(meta => meta!.ActionId!.Value).Distinct().ToList();
        var cards = actionIds.Count == 0
            ? new Dictionary<Guid, AssistantPendingActionDto>()
            : (await _actionCards.GetCardsAsync(userId, actionIds, cancellationToken)).ToDictionary(pair => pair.Key, pair => pair.Value);

        return messages.Select(message =>
        {
            var meta = metas[message];
            AssistantPendingActionDto? card = meta?.ActionId is { } id && cards.TryGetValue(id, out var found) ? found : null;
            return new AssistantMessageDto(
                message.Id,
                message.Role == AssistantMessageRole.User ? "User" : "Assistant",
                message.Content,
                meta?.Kind ?? AssistantMessageKind.Text,
                DateTime.SpecifyKind(message.CreatedAt, DateTimeKind.Utc),
                message.ProviderKind,
                meta?.Links ?? [],
                meta?.RankedTasks ?? [],
                card);
        }).ToList();
    }

    private AssistantMessageDto Transient(string content, AssistantMessageKind kind) =>
        new(0, "Assistant", content, kind, UtcNow, null, [], [], null);
}
