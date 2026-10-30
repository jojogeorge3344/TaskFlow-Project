using System.Security.Claims;
using System.Text.Json;
using TaskFlow.Application.Assistant;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Infrastructure.Assistant;

namespace TaskFlow.UnitTests;

/// <summary>Mirrors two of the API's permission policies (roles.manage, tasks.create) by role, so the gate can be tested without the web
/// host. The integration tests check the real policies against the real authorizer.</summary>
internal sealed class RolePolicyAuthorizer : IAssistantAuthorizer
{
    public const string ManagerPolicy = "perm:tasks.create";

    public Dictionary<string, string[]> Policies { get; } = new()
    {
        [AssistantPolicies.RolesManage] = ["Administrator"],
        [ManagerPolicy] = ["Administrator", "Manager"]
    };

    public Task<bool> IsAllowedAsync(ClaimsPrincipal user, string? policyName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(policyName))
        {
            return Task.FromResult(user.Identity?.IsAuthenticated == true);
        }

        var allowed = Policies.TryGetValue(policyName, out var roles) && roles.Any(user.IsInRole);
        return Task.FromResult(allowed);
    }
}

internal sealed class StubTool : IAssistantTool
{
    public StubTool(string name, string? policy, string action = "Doing the thing", string who = "an authorized administrator", AssistantActionKind? actionKind = null)
    {
        Name = name;
        Policy = policy;
        ActionDescription = action;
        WhoCanDoIt = who;
        ActionKind = actionKind;
    }

    public string Name { get; }
    public string Description => "stub";
    public string ParametersSchema => """{"type":"object","properties":{}}""";
    public string? Policy { get; }
    public string ActionDescription { get; }
    public string WhoCanDoIt { get; }
    public AssistantActionKind? ActionKind { get; }
    public int Runs { get; private set; }
    public JsonElement? LastArguments { get; private set; }

    public Task<ToolOutcome> ExecuteAsync(AssistantCaller caller, int conversationId, JsonElement arguments, CancellationToken cancellationToken)
    {
        Runs++;
        LastArguments = arguments;
        return Task.FromResult(new ToolOutcome("ran"));
    }
}

public class AssistantToolGateTests
{
    private static AssistantCaller Caller(string role) =>
        new(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, role)], "test")));

    private static readonly RecordingAudit Audit = new();

    private static (AssistantToolbox Toolbox, StubTool Open, StubTool Managers, StubTool Admins, RolePolicyAuthorizer Authorizer) Create()
    {
        var authorizer = new RolePolicyAuthorizer();
        var open = new StubTool("open_tool", null);
        var managers = new StubTool("manager_tool", RolePolicyAuthorizer.ManagerPolicy);
        var admins = new StubTool("admin_tool", AssistantPolicies.RolesManage, "Changing a user's role", actionKind: AssistantActionKind.ChangeUserRole);
        return (new AssistantToolbox([open, managers, admins], new AssistantToolGate(authorizer), Audit), open, managers, admins, authorizer);
    }

    private static ChatToolCall Call(string name, string args = "{}") => new("c1", name, args);

    [Theory]
    [InlineData("Employee", new[] { "open_tool" })]
    [InlineData("Manager", new[] { "open_tool", "manager_tool" })]
    [InlineData("Administrator", new[] { "open_tool", "manager_tool", "admin_tool" })]
    public async Task OnlyToolsTheRoleMayUseAreAdvertised(string role, string[] expected)
    {
        var (toolbox, _, _, _, _) = Create();

        var definitions = await toolbox.DefinitionsForAsync(Caller(role));

        Assert.Equal(expected.OrderBy(name => name), definitions.Select(definition => definition.Name).OrderBy(name => name));
    }

    [Theory]
    [InlineData("Employee")]
    [InlineData("Manager")]
    public async Task AnAdminToolIsRefusedForNonAdministratorsWithAFixedMessage(string role)
    {
        var (toolbox, _, _, admins, _) = Create();

        var outcome = await toolbox.ExecuteAsync(Caller(role), 1, Call("admin_tool"));

        Assert.Equal(0, admins.Runs);
        Assert.Equal(AssistantMessageKind.Refusal, outcome.FixedKind);
        Assert.Equal(
            "You do not have the required permission to perform this action. Changing a user's role can only be performed by an authorized administrator. Please contact your system administrator for assistance.",
            outcome.FixedReply);
    }

    [Fact]
    public async Task TheRefusalSaysWhatWhoAndWhatToDoNext()
    {
        var (toolbox, _, _, _, _) = Create();

        var outcome = await toolbox.ExecuteAsync(Caller("Employee"), 1, Call("admin_tool"));

        Assert.Contains("do not have the required permission", outcome.FixedReply);
        Assert.Contains("Changing a user's role", outcome.FixedReply);
        Assert.Contains("authorized administrator", outcome.FixedReply);
        Assert.Contains("contact your system administrator", outcome.FixedReply);
    }

    [Fact]
    public async Task AllowedToolsRunWithTheirArguments()
    {
        var (toolbox, _, managers, _, _) = Create();

        var outcome = await toolbox.ExecuteAsync(Caller("Manager"), 1, Call("manager_tool", """{"a":1}"""));

        Assert.Equal(1, managers.Runs);
        Assert.Null(outcome.FixedReply);
        Assert.Equal(1, managers.LastArguments!.Value.GetProperty("a").GetInt32());
    }

    [Fact]
    public async Task TheModelCannotCallAToolItWasNeverOfferedBecauseTheGateRunsOnEveryCall()
    {
        var (toolbox, _, _, admins, _) = Create();
        var offered = await toolbox.DefinitionsForAsync(Caller("Employee"));

        Assert.DoesNotContain(offered, definition => definition.Name == "admin_tool");
        var outcome = await toolbox.ExecuteAsync(Caller("Employee"), 1, Call("admin_tool"));

        Assert.Equal(0, admins.Runs);
        Assert.NotNull(outcome.FixedReply);
    }

    [Fact]
    public async Task APolicyChangeChangesTheGateResult()
    {
        var (toolbox, _, _, admins, authorizer) = Create();
        Assert.NotNull((await toolbox.ExecuteAsync(Caller("Manager"), 1, Call("admin_tool"))).FixedReply);

        authorizer.Policies[AssistantPolicies.RolesManage] = ["Administrator", "Manager"];
        var outcome = await toolbox.ExecuteAsync(Caller("Manager"), 1, Call("admin_tool"));

        Assert.Null(outcome.FixedReply);
        Assert.Equal(1, admins.Runs);
    }

    [Fact]
    public async Task ModelOutputCannotGrantPermission()
    {
        var (toolbox, _, _, admins, _) = Create();

        // Whatever the arguments claim, the caller's own token decides.
        var outcome = await toolbox.ExecuteAsync(
            Caller("Employee"), 1, Call("admin_tool", """{"role":"Administrator","isAdmin":true,"userId":1,"approvedBy":"admin"}"""));

        Assert.Equal(0, admins.Runs);
        Assert.Equal(AssistantMessageKind.Refusal, outcome.FixedKind);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerIsRefusedEverything()
    {
        var (toolbox, open, _, _, _) = Create();
        var anonymous = new AssistantCaller(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7")])));

        var outcome = await toolbox.ExecuteAsync(anonymous, 1, Call("open_tool"));

        Assert.Equal(0, open.Runs);
        Assert.NotNull(outcome.FixedReply);
    }

    [Fact]
    public async Task UnknownToolsAndBadArgumentsAreHandledWithoutRunningAnything()
    {
        var (toolbox, open, _, _, _) = Create();

        var unknown = await toolbox.ExecuteAsync(Caller("Employee"), 1, Call("does_not_exist"));
        var badJson = await toolbox.ExecuteAsync(Caller("Employee"), 1, Call("open_tool", "{not json"));
        var notObject = await toolbox.ExecuteAsync(Caller("Employee"), 1, Call("open_tool", "[1,2]"));

        Assert.Contains("no tool named", unknown.ResultForModel);
        Assert.Contains("not valid JSON", badJson.ResultForModel);
        Assert.Contains("JSON object", notObject.ResultForModel);
        Assert.Equal(0, open.Runs);
    }

    [Fact]
    public async Task ARefusedActionToolIsRecordedInTheAuditLogButAReadToolIsNot()
    {
        var (toolbox, _, _, _, _) = Create();
        Audit.Entries.Clear();

        await toolbox.ExecuteAsync(Caller("Employee"), 1, Call("admin_tool"));
        await toolbox.ExecuteAsync(Caller("Employee"), 1, Call("open_tool"));

        var entry = Assert.Single(Audit.Entries);
        Assert.Equal((7, AssistantActionKind.ChangeUserRole, "refused"), entry);
    }

    [Fact]
    public void ThePolicyNamesMatchTheApisPolicyNames()
    {
        Assert.Equal("perm:roles.manage", AssistantPolicies.RolesManage);
    }
}
