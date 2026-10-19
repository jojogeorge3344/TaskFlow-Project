using System.Text.Json;
using TaskFlow.Domain.Security;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Assistant;
using TaskFlow.Application.Mail.Validators;
using TaskFlow.Application.Users.Validators;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Assistant;
using Task = System.Threading.Tasks.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;
using User = TaskFlow.Domain.Entities.User;

namespace TaskFlow.UnitTests;

public class AssistantActionToolsTests
{
    private static readonly System.Text.Json.JsonSerializerOptions PayloadJson = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static DraftMailTool DraftTool(AssistantActionHarness h) =>
        new(h.Resolver, h.Managers, h.Actions, new SendMailRequestValidator());

    private static ProposeProfileUpdateTool ProfileTool(AssistantActionHarness h) =>
        new(h.Db, h.Actions, new UpdateOwnProfileRequestValidator());

    private static ProposeRoleChangeTool RoleTool(AssistantActionHarness h) => new(h.Resolver, h.Actions);

    private static Task<ToolOutcome> Run(IAssistantTool tool, AssistantActionHarness h, User user, string args) =>
        tool.ExecuteAsync(AssistantActionHarness.Caller(user), h.Conversation(user).Id, Args(args), CancellationToken.None);

    private static async Task<SendMailPayload> PayloadAsync(AssistantActionHarness h, ToolOutcome outcome)
    {
        var action = await h.Db.AssistantPendingActions.AsNoTracking().SingleAsync(a => a.Id == outcome.PendingActionId);
        return System.Text.Json.JsonSerializer.Deserialize<SendMailPayload>(action.PayloadJson, PayloadJson)!;
    }

    // ------------------------------------------------------------------ draft_mail and the manager rule

    [Fact]
    public async Task MailToMyManagerGoesToTheTaskCreatorWhenThatIsADifferentActiveUser()
    {
        await using var h = new AssistantActionHarness();
        var task = h.Tracking.AddTask(h.Asha.Id, h.Ravi.Id, "Fix login", TaskStatus.InProgress);

        var outcome = await Run(DraftTool(h), h, h.Asha,
            $$"""{"recipient":"manager","taskId":{{task.Id}},"subject":"Update on the login bug","body":"It is in review."}""");

        Assert.NotNull(outcome.PendingActionId);
        var payload = await PayloadAsync(h, outcome);
        Assert.Equal(h.Ravi.Id, Assert.Single(payload.To).UserId);
        Assert.Equal(task.Id, payload.TaskId);
        Assert.Equal(AssistantTexts.ActionPrepared, outcome.FixedReply);
    }

    [Fact]
    public async Task WhenTheSenderCreatedTheTaskTheDepartmentsManagerIsUsed()
    {
        await using var h = new AssistantActionHarness();
        var task = h.Tracking.AddTask(h.Asha.Id, h.Asha.Id, "My own task");

        var outcome = await Run(DraftTool(h), h, h.Asha,
            $$"""{"recipient":"manager","taskId":{{task.Id}},"subject":"Question","body":"Can we talk?"}""");

        var payload = await PayloadAsync(h, outcome);
        Assert.Equal(h.Ravi.Id, Assert.Single(payload.To).UserId);
    }

    [Fact]
    public async Task WithoutATaskTheDepartmentManagerIsUsed()
    {
        await using var h = new AssistantActionHarness();

        var outcome = await Run(DraftTool(h), h, h.Asha, """{"recipient":"manager","subject":"Hi","body":"Hello."}""");

        Assert.Equal(h.Ravi.Id, Assert.Single((await PayloadAsync(h, outcome)).To).UserId);
    }

    [Fact]
    public async Task SeveralManagersInTheDepartmentMeansTheUserIsAsked()
    {
        await using var h = new AssistantActionHarness();
        var kiran = h.AddUser("kiran", SystemRoles.Manager, h.Engineering.Id);

        var outcome = await Run(DraftTool(h), h, h.Asha, """{"recipient":"manager","subject":"Hi","body":"Hello."}""");

        Assert.Null(outcome.PendingActionId);
        Assert.Null(outcome.FixedReply);
        Assert.Contains("more than one manager", outcome.ResultForModel);
        Assert.Contains(kiran.Email, outcome.ResultForModel);
        Assert.Contains(h.Ravi.Email, outcome.ResultForModel);
        Assert.Equal(0, await h.Db.AssistantPendingActions.CountAsync());
    }

    [Fact]
    public async Task NoManagerFoundMeansTheUserIsAsked()
    {
        await using var h = new AssistantActionHarness();
        var loner = h.AddUser("loner", SystemRoles.Employee);
        var noManagers = h.AddUser("othersdept", SystemRoles.Employee, (await AddDepartmentAsync(h, "Support")).Id);

        var noDepartment = await Run(DraftTool(h), h, loner, """{"recipient":"manager","subject":"Hi","body":"Hello."}""");
        var emptyDepartment = await Run(DraftTool(h), h, noManagers, """{"recipient":"manager","subject":"Hi","body":"Hello."}""");

        Assert.Null(noDepartment.PendingActionId);
        Assert.Contains("not in a project", noDepartment.ResultForModel);
        Assert.Null(emptyDepartment.PendingActionId);
        Assert.Contains("could not find a manager", emptyDepartment.ResultForModel);
        Assert.Contains("Ask the user", emptyDepartment.ResultForModel);
    }

    private static async Task<TaskFlow.Domain.Entities.Department> AddDepartmentAsync(AssistantActionHarness h, string name)
    {
        var department = new TaskFlow.Domain.Entities.Department { Name = name, CreatedAt = DateTime.UtcNow };
        h.Db.Departments.Add(department);
        await h.Db.SaveChangesAsync();
        return department;
    }

    [Fact]
    public async Task ATaskTheUserCannotAccessIsReportedAsNotFound()
    {
        await using var h = new AssistantActionHarness();
        var private_ = h.Tracking.AddTask(h.Ravi.Id, h.Admin.Id, "Management only");

        var outcome = await Run(DraftTool(h), h, h.Asha,
            $$"""{"recipient":"manager","taskId":{{private_.Id}},"subject":"Hi","body":"Hello."}""");

        Assert.Null(outcome.PendingActionId);
        Assert.Contains("not found, or you do not have access", outcome.ResultForModel);
    }

    [Fact]
    public async Task RecipientsByNameAndEmailAreResolvedOnTheServer()
    {
        await using var h = new AssistantActionHarness();

        var byName = await Run(DraftTool(h), h, h.Asha, """{"to":["priya"],"subject":"Lunch","body":"Noon?"}""");
        var byEmail = await Run(DraftTool(h), h, h.Asha, $$"""{"to":["{{h.Ravi.Email}}"],"subject":"Lunch","body":"Noon?"}""");

        Assert.Equal(h.Priya.Id, Assert.Single((await PayloadAsync(h, byName)).To).UserId);
        Assert.Equal(h.Ravi.Id, Assert.Single((await PayloadAsync(h, byEmail)).To).UserId);
    }

    [Fact]
    public async Task AnUnknownInactiveOrAmbiguousRecipientIsNotMailed()
    {
        await using var h = new AssistantActionHarness();
        h.AddUser("dormant", SystemRoles.Employee, h.Engineering.Id, isActive: false);

        var unknown = await Run(DraftTool(h), h, h.Asha, """{"to":["Zed Zebra"],"subject":"Hi","body":"Hello."}""");
        var inactive = await Run(DraftTool(h), h, h.Asha, """{"to":["dormant"],"subject":"Hi","body":"Hello."}""");
        var ambiguous = await Run(DraftTool(h), h, h.Asha, """{"to":["full name"],"subject":"Hi","body":"Hello."}""");
        var outsider = await Run(DraftTool(h), h, h.Asha, """{"to":["someone@outside.example"],"subject":"Hi","body":"Hello."}""");

        Assert.All(new[] { unknown, inactive, ambiguous, outsider }, outcome =>
        {
            Assert.Null(outcome.PendingActionId);
            Assert.Null(outcome.FixedReply);
        });
        Assert.Contains("could not find", unknown.ResultForModel);
        Assert.Contains("could not find", inactive.ResultForModel);
        Assert.Contains("More than one person", ambiguous.ResultForModel);
        Assert.Contains("could not find", outsider.ResultForModel);
        Assert.Equal(0, await h.Db.AssistantPendingActions.CountAsync());
    }

    [Fact]
    public async Task ADraftIsOnlyPreparedAndNeverSent()
    {
        await using var h = new AssistantActionHarness();

        var outcome = await Run(DraftTool(h), h, h.Asha, """{"to":["ravi"],"subject":"Hi","body":"Hello."}""");

        Assert.NotNull(outcome.PendingActionId);
        Assert.Contains("has not been sent", outcome.ResultForModel);
        Assert.Equal(0, await h.Db.MailMessages.CountAsync());
        Assert.Equal(AssistantActionStatus.Pending, (await h.Db.AssistantPendingActions.SingleAsync()).Status);
    }

    [Fact]
    public async Task AnEmptyDraftAndDuplicateRecipientsAreHandled()
    {
        await using var h = new AssistantActionHarness();

        var empty = await Run(DraftTool(h), h, h.Asha, """{"to":["ravi"],"subject":"","body":""}""");
        var twice = await Run(DraftTool(h), h, h.Asha, """{"to":["ravi", "ravi"],"subject":"Hi","body":"Hello."}""");
        var nobody = await Run(DraftTool(h), h, h.Asha, """{"subject":"Hi","body":"Hello."}""");

        Assert.Null(empty.PendingActionId);
        Assert.Contains("not valid", empty.ResultForModel);
        Assert.Single((await PayloadAsync(h, twice)).To);
        Assert.Null(nobody.PendingActionId);
        Assert.Contains("no recipient", nobody.ResultForModel);
    }

    // ------------------------------------------------------------------ propose_profile_update

    [Fact]
    public async Task AProfileChangeIsPreparedForConfirmationAndNothingChangesYet()
    {
        await using var h = new AssistantActionHarness();

        var outcome = await Run(ProfileTool(h), h, h.Asha, """{"fullName":"Asha Nair"}""");

        Assert.NotNull(outcome.PendingActionId);
        var card = (await h.Actions.GetCardsAsync(h.Asha.Id, [outcome.PendingActionId!.Value])).Values.Single();
        Assert.Equal("Name", Assert.Single(card.Fields).Label);
        Assert.Contains("→ Asha Nair", card.Fields[0].Value);
        Assert.Equal("asha full name", (await h.Db.Users.AsNoTracking().SingleAsync(u => u.Id == h.Asha.Id)).FullName);
    }

    [Fact]
    public async Task OnlyNameAndEmailCanBeChangedWhateverTheModelAsksFor()
    {
        await using var h = new AssistantActionHarness();
        var caller = AssistantActionHarness.Caller(h.Asha);

        var outcome = await Run(ProfileTool(h), h, h.Asha,
            """{"fullName":"Asha Nair","role":"Administrator","departmentId":99,"isActive":false,"userId":1}""");
        await h.Actions.ConfirmAsync(caller, outcome.PendingActionId!.Value);

        var user = await h.Db.Users.AsNoTracking().SingleAsync(u => u.Id == h.Asha.Id);
        Assert.Equal("Asha Nair", user.FullName);
        Assert.Equal(SystemRoles.EmployeeId, user.RoleId);
        Assert.Equal(h.Engineering.Id, user.DepartmentId);
        Assert.True(user.IsActive);
        Assert.Equal(h.Asha.Email, user.Email);
        using var schema = JsonDocument.Parse(ProfileTool(h).ParametersSchema);
        Assert.Equal(["email", "fullName"], schema.RootElement.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task AToolForOwnProfileCannotTargetAnotherUser()
    {
        await using var h = new AssistantActionHarness();

        var outcome = await Run(ProfileTool(h), h, h.Asha, $$"""{"userId":{{h.Priya.Id}},"fullName":"Hacked Name"}""");

        var action = await h.Db.AssistantPendingActions.AsNoTracking().SingleAsync();
        Assert.Equal(h.Asha.Id, action.UserId);
        Assert.Equal("Hacked Name", JsonDocument.Parse(action.PayloadJson).RootElement.GetProperty("fullName").GetString());
        await h.Actions.ConfirmAsync(AssistantActionHarness.Caller(h.Asha), outcome.PendingActionId!.Value);
        Assert.Equal("priya full name", (await h.Db.Users.AsNoTracking().SingleAsync(u => u.Id == h.Priya.Id)).FullName);
        Assert.Equal("Hacked Name", (await h.Db.Users.AsNoTracking().SingleAsync(u => u.Id == h.Asha.Id)).FullName);
    }

    [Fact]
    public async Task InvalidOrDuplicateProfileValuesAreNotPrepared()
    {
        await using var h = new AssistantActionHarness();

        var nothing = await Run(ProfileTool(h), h, h.Asha, "{}");
        var badEmail = await Run(ProfileTool(h), h, h.Asha, """{"email":"not-an-email"}""");
        var taken = await Run(ProfileTool(h), h, h.Asha, $$"""{"email":"{{h.Priya.Email}}"}""");

        Assert.All(new[] { nothing, badEmail, taken }, outcome => Assert.Null(outcome.PendingActionId));
        Assert.Contains("Ask the user", nothing.ResultForModel);
        Assert.Contains("not valid", badEmail.ResultForModel);
        Assert.Contains("already used", taken.ResultForModel);
    }

    // ------------------------------------------------------------------ propose_role_change

    private static AssistantToolbox Toolbox(AssistantActionHarness h, RecordingAudit? audit = null) =>
        new([RoleTool(h), ProfileTool(h)], new AssistantToolGate(h.Authorizer), audit ?? new RecordingAudit());

    [Theory]
    [InlineData("Employee")]
    [InlineData("Manager")]
    public async Task ANonAdministratorAskingForARoleChangeIsRefusedAndNothingChanges(string role)
    {
        await using var h = new AssistantActionHarness();
        var user = role == "Employee" ? h.Asha : h.Ravi;
        var audit = new RecordingAudit();

        var outcome = await Toolbox(h, audit).ExecuteAsync(
            AssistantActionHarness.Caller(user), h.Conversation(user).Id,
            new ChatToolCall("c1", "propose_role_change", $$"""{"user":"{{user.Username}}","role":"Administrator"}"""));

        Assert.Equal(AssistantMessageKind.Refusal, outcome.FixedKind);
        Assert.Equal(
            "You do not have the required permission to perform this action. Changing a user's role can only be performed by an authorized administrator. Please contact your system administrator for assistance.",
            outcome.FixedReply);
        Assert.Equal(user.RoleId, (await h.Db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).RoleId);
        Assert.Equal(0, await h.Db.AssistantPendingActions.CountAsync());
        Assert.Equal([(user.Id, AssistantActionKind.ChangeUserRole, "refused")], audit.Entries);
    }

    [Fact]
    public async Task TheRoleToolIsNotOfferedToNonAdministrators()
    {
        await using var h = new AssistantActionHarness();
        var toolbox = Toolbox(h);

        var forEmployee = await toolbox.DefinitionsForAsync(AssistantActionHarness.Caller(h.Asha));
        var forAdmin = await toolbox.DefinitionsForAsync(AssistantActionHarness.Caller(h.Admin));

        Assert.DoesNotContain(forEmployee, d => d.Name == "propose_role_change");
        Assert.Contains(forAdmin, d => d.Name == "propose_role_change");
    }

    [Fact]
    public async Task AnAdministratorGetsARoleChangeCardAndNothingChangesUntilConfirmed()
    {
        await using var h = new AssistantActionHarness();

        var outcome = await Run(RoleTool(h), h, h.Admin, """{"user":"priya","role":"Manager"}""");

        Assert.NotNull(outcome.PendingActionId);
        var card = (await h.Actions.GetCardsAsync(h.Admin.Id, [outcome.PendingActionId!.Value])).Values.Single();
        Assert.Equal("Change role", card.Title);
        Assert.Equal("Employee → Manager", card.Fields.Single(f => f.Label == "Role").Value);
        Assert.Equal(SystemRoles.EmployeeId, (await h.Db.Users.AsNoTracking().SingleAsync(u => u.Id == h.Priya.Id)).RoleId);
    }

    [Fact]
    public async Task RoleChangeEdgeCasesAreHandledWithoutACard()
    {
        await using var h = new AssistantActionHarness();

        var self = await Run(RoleTool(h), h, h.Admin, """{"user":"admin","role":"Employee"}""");
        var same = await Run(RoleTool(h), h, h.Admin, """{"user":"priya","role":"Employee"}""");
        var unknown = await Run(RoleTool(h), h, h.Admin, """{"user":"nobody","role":"Manager"}""");
        var ambiguous = await Run(RoleTool(h), h, h.Admin, """{"user":"full name","role":"Manager"}""");
        var badRole = await Run(RoleTool(h), h, h.Admin, """{"user":"priya","role":"Overlord"}""");

        Assert.Contains("own role", self.FixedReply);
        Assert.Contains("already a Employee", same.ResultForModel);
        Assert.Contains("could not find", unknown.ResultForModel);
        Assert.Contains("More than one person", ambiguous.ResultForModel);
        Assert.Contains("That role does not exist", badRole.ResultForModel);
        Assert.Equal(0, await h.Db.AssistantPendingActions.CountAsync());
    }
}
