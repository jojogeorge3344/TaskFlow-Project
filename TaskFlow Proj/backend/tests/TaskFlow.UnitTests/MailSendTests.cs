using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Mail;
using TaskFlow.Application.Mail.Validators;
using TaskFlow.Domain.Enums;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

public class MailSendTests
{
    [Fact]
    public async Task Mail_reaches_to_and_cc_recipients_and_the_senders_sent_folder()
    {
        await using var mail = MailTestHarness.Create();

        var sent = await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya], cc: [mail.Kiran]));

        Assert.Equal(MailFolder.Sent, sent.Folder);
        Assert.True(sent.IsRead);
        Assert.Equal([mail.Priya.Email], sent.Message.To.Select(person => person.Email));
        Assert.Equal([mail.Kiran.Email], sent.Message.Cc.Select(person => person.Email));
        Assert.Equal(mail.Ravi.FullName, sent.Message.From.Name);

        var priyaInbox = Assert.Single(await mail.FolderAsync(mail.Priya, MailView.Inbox));
        Assert.False(priyaInbox.IsRead);
        Assert.Equal("Weekly report", priyaInbox.Subject);
        Assert.Single(await mail.FolderAsync(mail.Kiran, MailView.Inbox));
        Assert.Single(await mail.FolderAsync(mail.Ravi, MailView.Sent));
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Inbox));
    }

    [Theory]
    [InlineData("someone@gmail.com")]
    [InlineData("nobody@taskflow.com")]
    public async Task Address_that_is_not_a_taskflow_user_rejects_the_whole_send(string outsider)
    {
        await using var mail = MailTestHarness.Create();

        var error = await Assert.ThrowsAsync<MailValidationException>(() =>
            mail.Mail.SendAsync(mail.Ravi.Id, new SendMailRequest([mail.Priya.Email, outsider], null, "Hi", "Body", null)));

        Assert.Contains(outsider, error.Errors["To"].Single());
        Assert.Equal(0, await mail.Tracking.Db.MailMessages.CountAsync());
        Assert.Equal(0, await mail.Tracking.Db.MailboxItems.CountAsync());
    }

    [Fact]
    public async Task Deactivated_and_deleted_users_cannot_receive_mail()
    {
        await using var mail = MailTestHarness.Create();
        mail.Priya.IsActive = false;
        mail.Kiran.IsDeleted = true;
        await mail.Tracking.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<MailValidationException>(() =>
            mail.Mail.SendAsync(mail.Ravi.Id, MailTestHarness.To([mail.Asha], cc: [mail.Priya, mail.Kiran])));

        Assert.Contains(mail.Priya.Email, error.Errors["Cc"].Single());
        Assert.Contains(mail.Kiran.Email, error.Errors["Cc"].Single());
        Assert.Empty(await mail.FolderAsync(mail.Asha, MailView.Inbox));
    }

    [Fact]
    public async Task Sending_needs_a_recipient_and_some_content()
    {
        await using var mail = MailTestHarness.Create();

        await Assert.ThrowsAsync<MailValidationException>(() =>
            mail.Mail.SendAsync(mail.Ravi.Id, new SendMailRequest([], null, "Hi", "Body", null)));
        await Assert.ThrowsAsync<MailValidationException>(() =>
            mail.Mail.SendAsync(mail.Ravi.Id, new SendMailRequest([mail.Priya.Email], null, " ", "  ", null)));
        Assert.Equal(0, await mail.Tracking.Db.MailMessages.CountAsync());
    }

    [Fact]
    public async Task Mailing_yourself_puts_it_in_sent_and_inbox()
    {
        await using var mail = MailTestHarness.Create();

        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Ravi]));

        Assert.Single(await mail.FolderAsync(mail.Ravi, MailView.Sent));
        Assert.False(Assert.Single(await mail.FolderAsync(mail.Ravi, MailView.Inbox)).IsRead);
    }

    [Fact]
    public async Task Addresses_are_case_insensitive_and_a_to_cc_duplicate_is_delivered_once_as_to()
    {
        await using var mail = MailTestHarness.Create();

        var sent = await mail.SendAsync(mail.Ravi, new SendMailRequest(
            [mail.Priya.Email.ToUpperInvariant(), " " + mail.Priya.Email],
            [mail.Priya.Email, mail.Kiran.Email],
            "Hi",
            "Body",
            null));

        Assert.Equal([mail.Priya.Id], sent.Message.To.Select(person => person.UserId));
        Assert.Equal([mail.Kiran.Id], sent.Message.Cc.Select(person => person.UserId));
        Assert.Single(await mail.FolderAsync(mail.Priya, MailView.Inbox));
    }

    [Fact]
    public void Validator_enforces_lengths_recipient_count_and_address_shape()
    {
        var validator = new SendMailRequestValidator();
        var valid = new SendMailRequest(["a@taskflow.com"], null, "Hi", "Body", null);

        validator.TestValidate(valid).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(valid with { Body = new string('x', MailRules.MaxBodyLength + 1) })
            .ShouldHaveValidationErrorFor(request => request.Body);
        validator.TestValidate(valid with { Subject = new string('x', MailRules.MaxSubjectLength + 1) })
            .ShouldHaveValidationErrorFor(request => request.Subject);
        validator.TestValidate(valid with { Subject = "", Body = " " }).IsValid.ShouldBeFalse();
        validator.TestValidate(valid with { To = [] }).IsValid.ShouldBeFalse();
        validator.TestValidate(valid with { To = Enumerable.Range(0, 51).Select(i => $"u{i}@taskflow.com").ToList() })
            .IsValid.ShouldBeFalse();
        validator.TestValidate(valid with { To = ["not an address"] }).IsValid.ShouldBeFalse();
        validator.TestValidate(valid with { AttachmentIds = [1, 2, 3, 4, 5, 6] })
            .ShouldHaveValidationErrorFor(request => request.AttachmentIds);
    }

    [Fact]
    public void Draft_send_does_not_need_recipients_or_content_in_the_request()
    {
        new SendMailRequestValidator()
            .TestValidate(new SendMailRequest(null, null, null, null, null, DraftId: 4))
            .ShouldNotHaveAnyValidationErrors();
    }
}

internal static class BoolAssertions
{
    public static void ShouldBeFalse(this bool value) => Assert.False(value);
}
