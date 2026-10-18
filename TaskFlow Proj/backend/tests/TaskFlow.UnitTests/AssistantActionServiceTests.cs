using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Assistant;
using TaskFlow.Domain.Enums;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

public class AssistantActionServiceTests
{
    private static async Task<int> InboxCountAsync(AssistantActionHarness h, TaskFlow.Domain.Entities.User user) =>
        await h.Db.MailboxItems.CountAsync(item => item.OwnerUserId == user.Id && item.Folder == MailFolder.Inbox);

    private static async Task<int> SentCountAsync(AssistantActionHarness h, TaskFlow.Domain.Entities.User user) =>
        await h.Db.MailboxItems.CountAsync(item => item.OwnerUserId == user.Id && item.Folder == MailFolder.Sent);

    [Fact]
    public async Task NothingIsSentUntilTheUserConfirms()
    {
        await using var h = new AssistantActionHarness();

        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);

        Assert.Equal(AssistantActionStatus.Pending, card.Status);
        Assert.Equal(0, await SentCountAsync(h, h.Asha));
        Assert.Equal(0, await InboxCountAsync(h, h.Ravi));
        Assert.Equal(["prepared"], await h.OutcomesAsync(h.Asha));
    }

    [Fact]
    public async Task ConfirmSendsTheMailAsTheUserThroughMail()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);

        var result = await h.Actions.ConfirmAsync(AssistantActionHarness.Caller(h.Asha), card.Id);

        Assert.Equal(AssistantActionStatus.Confirmed, result!.Status);
        Assert.Contains("was sent to", result.Message);
        Assert.Equal(1, await SentCountAsync(h, h.Asha));
        Assert.Equal(1, await InboxCountAsync(h, h.Ravi));
        var sent = await h.Db.MailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(h.Asha.Id, sent.SenderUserId);
        Assert.Equal("Update on TF-102", sent.Subject);
        Assert.Equal(["prepared", "confirmed"], await h.OutcomesAsync(h.Asha));
        Assert.Equal(result.Message, result.Reply!.Content);
    }

    [Fact]
    public async Task AnotherUserCannotConfirmOrSeeOrCancelTheAction()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        var priya = AssistantActionHarness.Caller(h.Priya);

        Assert.Null(await h.Actions.ConfirmAsync(priya, card.Id));
        Assert.Null(await h.Actions.CancelAsync(priya, card.Id));
        Assert.Null(await h.Actions.EditMailAsync(priya, card.Id, new EditMailActionRequest(null, "hijack", null)));
        Assert.Empty(await h.Actions.GetCardsAsync(h.Priya.Id, [card.Id]));
        Assert.Equal(0, await SentCountAsync(h, h.Asha));
        Assert.Equal(AssistantActionStatus.Pending, (await h.Db.AssistantPendingActions.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task AnExpiredActionIsRefusedAndNothingIsSent()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        h.Clock.Set(h.Clock.GetUtcNow().UtcDateTime.AddMinutes(11));

        var error = await Assert.ThrowsAsync<AssistantActionConflictException>(
            () => h.Actions.ConfirmAsync(AssistantActionHarness.Caller(h.Asha), card.Id));

        Assert.Contains("expired", error.Message);
        Assert.Contains("ask TFA again", error.Message);
        Assert.Equal(0, await SentCountAsync(h, h.Asha));
        Assert.Equal(AssistantActionStatus.Expired, (await h.Db.AssistantPendingActions.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(["prepared", "expired"], await h.OutcomesAsync(h.Asha));
    }

    [Fact]
    public async Task ACardPastItsExpiryShowsAsExpired()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        h.Clock.Set(h.Clock.GetUtcNow().UtcDateTime.AddMinutes(11));

        var shown = (await h.Actions.GetCardsAsync(h.Asha.Id, [card.Id]))[card.Id];

        Assert.Equal(AssistantActionStatus.Expired, shown.Status);
        Assert.False(shown.CanEdit);
    }

    [Fact]
    public async Task ConfirmingTwiceDoesNotPerformTheActionAgain()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        var caller = AssistantActionHarness.Caller(h.Asha);
        await h.Actions.ConfirmAsync(caller, card.Id);

        var error = await Assert.ThrowsAsync<AssistantActionConflictException>(() => h.Actions.ConfirmAsync(caller, card.Id));

        Assert.Contains("already been done", error.Message);
        Assert.Equal(1, await InboxCountAsync(h, h.Ravi));
        Assert.Equal(1, await h.Db.MailMessages.CountAsync());
    }

    [Fact]
    public async Task APendingMailIsClaimedOnlyOnceEvenIfAnotherRequestGotThereFirst()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        var caller = AssistantActionHarness.Caller(h.Asha);

        // Another request wins the race between the check and the claim.
        var first = h.Actions;
        var second = h.Actions;
        await first.ConfirmAsync(caller, card.Id);

        await Assert.ThrowsAsync<AssistantActionConflictException>(() => second.ConfirmAsync(caller, card.Id));
        Assert.Equal(1, await h.Db.MailMessages.CountAsync());
    }

    [Fact]
    public async Task ThePermissionIsCheckedAgainAtConfirmation()
    {
        await using var h = new AssistantActionHarness();
        var conversation = h.Conversation(h.Admin);
        var card = await h.Actions.CreateAsync(
            AssistantActionHarness.Caller(h.Admin),
            conversation.Id,
            AssistantActionKind.ChangeUserRole,
            new ChangeRolePayload(h.Asha.Id, h.Asha.FullName, Role.Employee, Role.Manager));

        // The admin was demoted after the action was prepared, so the token they confirm with is an Employee's.
        var result = await h.Actions.ConfirmAsync(AssistantActionHarness.Caller(h.Admin, roleOverride: "Employee"), card.Id);

        Assert.Equal(AssistantActionStatus.Refused, result!.Status);
        Assert.Contains("authorized administrator", result.Message);
        Assert.Equal(AssistantMessageKind.Refusal, result.Reply!.Kind);
        Assert.Equal(Role.Employee, (await h.Db.Users.AsNoTracking().SingleAsync(user => user.Id == h.Asha.Id)).Role);
        Assert.Equal(["prepared", "refused"], await h.OutcomesAsync(h.Admin));
    }

    [Fact]
    public async Task CancelStopsTheActionFromBeingConfirmed()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        var caller = AssistantActionHarness.Caller(h.Asha);

        var cancelled = await h.Actions.CancelAsync(caller, card.Id);
        var error = await Assert.ThrowsAsync<AssistantActionConflictException>(() => h.Actions.ConfirmAsync(caller, card.Id));

        Assert.Equal(AssistantActionStatus.Cancelled, cancelled!.Status);
        Assert.Contains("cancelled", error.Message);
        Assert.Equal(0, await h.Db.MailMessages.CountAsync());
        Assert.Equal(["prepared", "cancelled"], await h.OutcomesAsync(h.Asha));
    }

    // ------------------------------------------------------------------ editing a mail

    [Fact]
    public async Task EditedSubjectBodyAndRecipientsAreWhatIsSent()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        var caller = AssistantActionHarness.Caller(h.Asha);

        var edited = await h.Actions.EditMailAsync(caller, card.Id, new EditMailActionRequest([h.Priya.Email], "Changed subject", "Changed body"));
        await h.Actions.ConfirmAsync(caller, card.Id);

        Assert.Equal("Changed body", edited!.Body);
        Assert.Contains("Changed subject", edited.Fields.Single(field => field.Label == "Subject").Value);
        var sent = await h.Db.MailMessages.AsNoTracking().SingleAsync();
        Assert.Equal("Changed subject", sent.Subject);
        Assert.Equal("Changed body", sent.Body);
        Assert.Equal(1, await InboxCountAsync(h, h.Priya));
        Assert.Equal(0, await InboxCountAsync(h, h.Ravi));
    }

    [Fact]
    public async Task EditingToAnUnknownRecipientIsRefusedAndTheOriginalIsKept()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        var caller = AssistantActionHarness.Caller(h.Asha);

        var error = await Assert.ThrowsAsync<AssistantRequestException>(
            () => h.Actions.EditMailAsync(caller, card.Id, new EditMailActionRequest(["nobody@outside.example"], null, null)));
        await h.Actions.ConfirmAsync(caller, card.Id);

        Assert.Contains("nobody@outside.example", error.Message);
        Assert.Equal(1, await InboxCountAsync(h, h.Ravi));
    }

    [Fact]
    public async Task EditingAnInvalidMailIsRefused()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        var caller = AssistantActionHarness.Caller(h.Asha);

        await Assert.ThrowsAsync<AssistantRequestException>(
            () => h.Actions.EditMailAsync(caller, card.Id, new EditMailActionRequest(null, "", "")));
        await Assert.ThrowsAsync<AssistantRequestException>(
            () => h.Actions.EditMailAsync(caller, card.Id, new EditMailActionRequest([], null, null)));
    }

    [Fact]
    public async Task AMailCannotBeEditedAfterItIsConfirmedOrIfItIsNotAMail()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        var caller = AssistantActionHarness.Caller(h.Asha);
        await h.Actions.ConfirmAsync(caller, card.Id);

        await Assert.ThrowsAsync<AssistantActionConflictException>(
            () => h.Actions.EditMailAsync(caller, card.Id, new EditMailActionRequest(null, "late", null)));

        var profile = await h.Actions.CreateAsync(caller, h.Conversation(h.Asha).Id, AssistantActionKind.UpdateOwnProfile,
            new UpdateProfilePayload("New Name", null, h.Asha.FullName, h.Asha.Email));
        await Assert.ThrowsAsync<AssistantRequestException>(
            () => h.Actions.EditMailAsync(caller, profile.Id, new EditMailActionRequest(null, "x", null)));
    }

    // ------------------------------------------------------------------ failures

    [Fact]
    public async Task ASendThatFailsIsReportedAsNotSentAndNeverAsSuccess()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        var ravi = await h.Db.Users.SingleAsync(user => user.Id == h.Ravi.Id);
        ravi.IsActive = false;
        await h.Db.SaveChangesAsync();

        var result = await h.Actions.ConfirmAsync(AssistantActionHarness.Caller(h.Asha), card.Id);

        Assert.Equal(AssistantActionStatus.Failed, result!.Status);
        Assert.Contains("I could not do that", result.Message);
        Assert.Contains("Nothing was sent", result.Message);
        Assert.DoesNotContain("Done", result.Message);
        Assert.Equal(0, await h.Db.MailMessages.CountAsync());
        Assert.Equal(AssistantActionStatus.Failed, (await h.Db.AssistantPendingActions.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(["prepared", "failed"], await h.OutcomesAsync(h.Asha));
    }

    [Fact]
    public async Task AnUnexpectedErrorWhileRunningTheActionLeavesItFailedAndNotConfirmed()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        // Make the database itself fail while the mail is being written: not an application rule, a real fault.
        await h.Db.Database.ExecuteSqlRawAsync("DROP TABLE MailRecipients");

        var result = await h.Actions.ConfirmAsync(AssistantActionHarness.Caller(h.Asha), card.Id);

        Assert.Equal(AssistantActionStatus.Failed, result!.Status);
        Assert.Contains("I could not do that", result.Message);
        Assert.Contains("Nothing was sent", result.Message);
        Assert.DoesNotContain("MailRecipients", result.Message);
        Assert.DoesNotContain("SQLite", result.Message);
        Assert.Equal(AssistantActionStatus.Failed, (await h.Db.AssistantPendingActions.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(["prepared", "failed"], await h.OutcomesAsync(h.Asha));
    }

    // ------------------------------------------------------------------ profile and role

    [Fact]
    public async Task ConfirmingAProfileChangeUpdatesOnlyNameAndEmail()
    {
        await using var h = new AssistantActionHarness();
        var caller = AssistantActionHarness.Caller(h.Asha);
        var card = await h.Actions.CreateAsync(caller, h.Conversation(h.Asha).Id, AssistantActionKind.UpdateOwnProfile,
            new UpdateProfilePayload("Asha Nair", "asha.nair@taskflow.local", h.Asha.FullName, h.Asha.Email));

        var result = await h.Actions.ConfirmAsync(caller, card.Id);

        Assert.Equal(AssistantActionStatus.Confirmed, result!.Status);
        var user = await h.Db.Users.AsNoTracking().SingleAsync(u => u.Id == h.Asha.Id);
        Assert.Equal("Asha Nair", user.FullName);
        Assert.Equal("asha.nair@taskflow.local", user.Email);
        Assert.Equal(Role.Employee, user.Role);
        Assert.Equal(h.Engineering.Id, user.DepartmentId);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task ARoleChangeByAnAdministratorChangesOnlyTheRoleAndEndsTheirSessions()
    {
        await using var h = new AssistantActionHarness();
        var before = await h.Db.Users.AsNoTracking().SingleAsync(u => u.Id == h.Priya.Id);
        var caller = AssistantActionHarness.Caller(h.Admin);
        var card = await h.Actions.CreateAsync(caller, h.Conversation(h.Admin).Id, AssistantActionKind.ChangeUserRole,
            new ChangeRolePayload(h.Priya.Id, h.Priya.FullName, Role.Employee, Role.Manager));

        var result = await h.Actions.ConfirmAsync(caller, card.Id);

        Assert.Equal(AssistantActionStatus.Confirmed, result!.Status);
        Assert.Contains("is now a Manager", result.Message);
        var after = await h.Db.Users.AsNoTracking().SingleAsync(u => u.Id == h.Priya.Id);
        Assert.Equal(Role.Manager, after.Role);
        Assert.Equal(before.FullName, after.FullName);
        Assert.Equal(before.Email, after.Email);
        Assert.Equal(before.Username, after.Username);
        Assert.Equal(before.DepartmentId, after.DepartmentId);
        Assert.Equal(before.IsActive, after.IsActive);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
    }

    [Fact]
    public async Task AFailureFromTheApplicationRulesIsReportedAndTheRoleIsUnchanged()
    {
        await using var h = new AssistantActionHarness();
        var caller = AssistantActionHarness.Caller(h.Admin);
        var card = await h.Actions.CreateAsync(caller, h.Conversation(h.Admin).Id, AssistantActionKind.ChangeUserRole,
            new ChangeRolePayload(99999, "Ghost", Role.Employee, Role.Manager));

        var result = await h.Actions.ConfirmAsync(caller, card.Id);

        Assert.Equal(AssistantActionStatus.Failed, result!.Status);
        Assert.Contains("not found", result.Message);
        Assert.Contains("Nothing was changed", result.Message);
    }

    // ------------------------------------------------------------------ cards and audit

    [Fact]
    public async Task CardsAreBuiltFromTheStoredPayload()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi, h.Priya);

        Assert.Equal("Mail draft", card.Title);
        Assert.Contains("ravi full name", card.Fields.Single(field => field.Label == "To").Value);
        Assert.Contains("priya@taskflow.local", card.Fields.Single(field => field.Label == "To").Value);
        Assert.Equal("It is in review.", card.Body);
        Assert.True(card.CanEdit);
        Assert.True(card.ExpiresAt > h.Clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task AuditRecordsHoldNoMailBodyPasswordOrKey()
    {
        await using var h = new AssistantActionHarness();
        var card = await h.PrepareMailAsync(h.Asha, h.Ravi);
        await h.Actions.ConfirmAsync(AssistantActionHarness.Caller(h.Asha), card.Id);

        var audits = await h.AuditFor(h.Asha).ToListAsync();

        Assert.All(audits, audit =>
        {
            Assert.DoesNotContain("in review", audit.Outcome);
            Assert.DoesNotContain("TF-102", audit.Outcome);
        });
        var properties = typeof(TaskFlow.Domain.Entities.AssistantAudit).GetProperties().Select(property => property.Name).ToList();
        Assert.Equal(["At", "Id", "Kind", "Outcome", "UserId"], properties.Order());
    }
}
