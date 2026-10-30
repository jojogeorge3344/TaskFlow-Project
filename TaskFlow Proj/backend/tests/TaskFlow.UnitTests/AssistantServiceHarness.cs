using TaskFlow.Api.Repositories.Assistant;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Services.Assistant;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Repositories.Settings.TfaSettings;
using TaskFlow.Api.Services.Settings.TfaSettings;
using TaskFlow.Api.Services.Interfaces.Assistant;
using TaskFlow.Api.Integrations.Interfaces.Ai;
using TaskFlow.Api.Integrations.Ai;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Services.Settings.ApplicationSettings;
using TaskFlow.Api.Config;
using System.Security.Claims;
using TaskFlow.Api.Common.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Models.Enums;
using User = TaskFlow.Api.Models.Entities.User;

namespace TaskFlow.UnitTests;

/// <summary>A gateway that answers from a script and records every request, so tests see exactly what each provider
/// was sent.</summary>
internal sealed class ScriptedGateway : IChatModelGateway
{
    public Func<AiProviderKind, ChatModelRequest, ChatModelResult> Handler { get; set; } = (_, _) => ChatModelResult.FromText("OK");

    public List<(AiProviderKind Kind, ChatModelRequest Request)> Calls { get; } = [];

    public Task<ChatModelResult> CompleteAsync(AiProviderKind kind, ChatModelRequest request, CancellationToken cancellationToken = default)
    {
        // The caller's list is mutated between rounds, so keep a snapshot of what was sent.
        Calls.Add((kind, request with { Messages = request.Messages.ToList() }));
        return Task.FromResult(Handler(kind, request));
    }
}

internal sealed class ScriptedToolbox : IAssistantToolbox
{
    public List<ChatToolDefinition> Definitions { get; } = [new("echo", "Echoes.", """{"type":"object","properties":{}}""")];

    public Func<ChatToolCall, ToolOutcome> Handler { get; set; } = call => new ToolOutcome($"result of {call.Name}");

    public List<ChatToolCall> Executed { get; } = [];

    public Task<IReadOnlyList<ChatToolDefinition>> DefinitionsForAsync(AssistantCaller caller, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ChatToolDefinition>>(Definitions);

    public Task<ToolOutcome> ExecuteAsync(AssistantCaller caller, int conversationId, ChatToolCall call, CancellationToken cancellationToken = default)
    {
        Executed.Add(call);
        return Task.FromResult(Handler(call));
    }
}

internal sealed class NoCards : IAssistantActionCards
{
    public Task<IReadOnlyDictionary<Guid, AssistantPendingActionDto>> GetCardsAsync(
        int userId, IReadOnlyCollection<Guid> actionIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, AssistantPendingActionDto>>(new Dictionary<Guid, AssistantPendingActionDto>());
}

internal sealed class AssistantServiceHarness : IAsyncDisposable
{
    private AssistantServiceHarness()
    {
        Tracking = TaskTrackingServiceHarness.Create();
        Asha = Tracking.AddUser("asha", SystemRoles.Employee);
        Ravi = Tracking.AddUser("ravi", SystemRoles.Manager);
        Config = new AiProviderConfigService(new AiProviderConfigRepository(Tracking.Db), new AiModelRepository(Tracking.Db), new UnitOfWork(Tracking.Db), new AiKeyProtector(new EphemeralDataProtectionProvider()), TimeProvider.System);
        RateLimiter = new AssistantRateLimiter(TimeProvider.System);
    }

    public TaskTrackingServiceHarness Tracking { get; }
    public User Asha { get; }
    public User Ravi { get; }
    public AiProviderConfigService Config { get; }
    public AssistantRateLimiter RateLimiter { get; set; }
    public ScriptedGateway Gateway { get; } = new();
    public ScriptedToolbox Toolbox { get; } = new();

    public static async Task<AssistantServiceHarness> CreateAsync()
    {
        var harness = new AssistantServiceHarness();
        await harness.Config.SaveAsync(1, new UpdateAiProvidersRequest(
        [
            new UpdateAiProviderRequest(AiProviderKind.Ollama, true, "llama3.1:8b", "http://localhost:11434", null, null),
            new UpdateAiProviderRequest(AiProviderKind.Gemini, true, "gemini-2.0-flash", null, "AIzaSyExampleKey1234", null)
        ]));
        return harness;
    }

    /// <summary>A fresh service each call, like a new request scope.</summary>
    public AssistantService Service =>
        new(
            new AssistantConversationRepository(Tracking.Db),
            new AssistantMessageRepository(Tracking.Db),
            new AssistantDirectoryRepository(Tracking.Db),
            new UnitOfWork(Tracking.Db),
            Gateway,
            Config,
            RateLimiter,
            Toolbox,
            new NoCards(),
            new ApplicationSettingsService(new ApplicationSettingsRepository(Tracking.Db), new UnitOfWork(Tracking.Db), TimeProvider.System, Options.Create(new ApplicationOptions())),
            Tracking.Clock,
            TimeProvider.System);

    public static AssistantCaller Caller(User user) =>
        new(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, TestAccessControl.NameFor(user.RoleId))
            ],
            "test")));

    public Task<AssistantReplyDto> SendAsync(User user, string text, AiProviderKind provider = AiProviderKind.Ollama) =>
        Service.SendAsync(Caller(user), new SendAssistantMessageRequest(text, provider));

    public ValueTask DisposeAsync() => Tracking.DisposeAsync();
}
