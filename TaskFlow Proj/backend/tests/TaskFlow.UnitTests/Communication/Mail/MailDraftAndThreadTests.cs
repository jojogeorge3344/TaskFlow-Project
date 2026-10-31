using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests.Communication.Mail;

public class MailDraftAndThreadTests
{
    // ------------------------------------------------------------------ drafts

    [Fact]
    public async Task Draft_is_private_until_sent_and_may_hold_unfinished_recipients()
    {
        await using var mail = MailTestHarness.Create();

        var draft = await mail.Mail.CreateDraftAsync(mail.Ravi.Id, new SaveDraftRequest([mail.Priya.Email, "not-yet"], null, "Plan", "Draft body", null));

        Assert.NotNull(draft);
        Assert.Equal(MailFolder.Drafts, draft.Folder);
        Assert.Equal($"{mail.Priya.Email}, not-yet", draft.DraftTo);
        Assert.Single(await mail.FolderAsync(mail.Ravi, MailView.Drafts));
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Sent));
        Assert.Empty(await mail.FolderAsync(mail.Priya, MailView.Inbox));
        Assert.Equal(1, (await mail.Mail.GetSummaryAsync(mail.Ravi.Id)).Drafts);
        Assert.Null(await mail.Mail.OpenAsync(mail.Priya.Id, draft.ItemId));
    }

    [Fact]
    public async Task Draft_can_be_edited_then_sent_and_moves_to_sent()
    {
        await using var mail = MailTestHarness.Create();
        var draft = await mail.Mail.CreateDraftAsync(mail.Ravi.Id, new SaveDraftRequest(["oops"], null, "Plan", "v1", null));

        var updated = await mail.Mail.UpdateDraftAsync(mail.Ravi.Id, draft!.Message.MessageId, new SaveDraftRequest([mail.Priya.Email], null, "Plan", "v2", null));
        Assert.Equal("v2", updated!.Message.Body);

        var sent = await mail.Mail.SendAsync(mail.Ravi.Id, new SendMailRequest(null, null, null, null, null, DraftId: draft.Message.MessageId));

        Assert.Equal(MailFolder.Sent, sent!.Folder);
        Assert.Equal("v2", sent.Message.Body);
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Drafts));
        Assert.Single(await mail.FolderAsync(mail.Ravi, MailView.Sent));
        Assert.Equal("Plan", Assert.Single(await mail.FolderAsync(mail.Priya, MailView.Inbox)).Subject);
        Assert.Null(sent.DraftTo);
    }

    [Fact]
    public async Task Sending_a_draft_applies_the_send_rules()
    {
        await using var mail = MailTestHarness.Create();
        var draft = await mail.Mail.CreateDraftAsync(mail.Ravi.Id, new SaveDraftRequest(["someone@gmail.com"], null, "Plan", "Body", null));

        await Assert.ThrowsAsync<MailValidationException>(() =>
            mail.Mail.SendAsync(mail.Ravi.Id, new SendMailRequest(null, null, null, null, null, DraftId: draft!.Message.MessageId)));

        Assert.Single(await mail.FolderAsync(mail.Ravi, MailView.Drafts));
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Sent));
    }

    [Fact]
    public async Task Only_the_author_can_edit_send_or_discard_a_draft()
    {
        await using var mail = MailTestHarness.Create();
        var draft = await mail.Mail.CreateDraftAsync(mail.Ravi.Id, new SaveDraftRequest([mail.Priya.Email], null, "Plan", "Body", null));
        var messageId = draft!.Message.MessageId;

        Assert.Null(await mail.Mail.UpdateDraftAsync(mail.Priya.Id, messageId, new SaveDraftRequest(null, null, "x", "y", null)));
        Assert.Null(await mail.Mail.SendAsync(mail.Priya.Id, new SendMailRequest(null, null, null, null, null, DraftId: messageId)));
        Assert.False(await mail.Mail.DeleteForeverAsync(mail.Priya.Id, draft.ItemId));

        Assert.True(await mail.Mail.DeleteForeverAsync(mail.Ravi.Id, draft.ItemId));
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Drafts));
        Assert.Null(await mail.Mail.SendAsync(mail.Ravi.Id, new SendMailRequest(null, null, null, null, null, DraftId: messageId)));
    }

    // ------------------------------------------------------------------ reply, reply all, forward

    [Fact]
    public async Task Reply_goes_to_the_sender_with_a_single_re_prefix_and_quote()
    {
        await using var mail = MailTestHarness.Create();
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya], "Weekly report", "Line A\nLine B"));
        var item = (await mail.FolderAsync(mail.Priya, MailView.Inbox)).Single();

        var compose = await mail.Mail.GetComposeAsync(mail.Priya.Id, item.ItemId, MailComposeMode.Reply);

        Assert.Equal([mail.Ravi.Email], compose!.To);
        Assert.Empty(compose.Cc);
        Assert.Equal("Re: Weekly report", compose.Subject);
        Assert.Contains($"{mail.Ravi.FullName} wrote:", compose.Body);
        Assert.Contains("> Line A", compose.Body);

        await mail.SendAsync(mail.Priya, new SendMailRequest(compose.To, null, compose.Subject, "Thanks" + compose.Body, null, ParentItemId: item.ItemId, Mode: MailComposeMode.Reply));
        var reply = (await mail.FolderAsync(mail.Ravi, MailView.Inbox)).Single();
        var again = await mail.Mail.GetComposeAsync(mail.Ravi.Id, reply.ItemId, MailComposeMode.Reply);
        Assert.Equal("Re: Weekly report", again!.Subject);
    }

    [Fact]
    public async Task Reply_all_includes_sender_and_other_recipients_but_not_me()
    {
        await using var mail = MailTestHarness.Create();
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya, mail.Asha], cc: [mail.Kiran]));
        var item = (await mail.FolderAsync(mail.Priya, MailView.Inbox)).Single();

        var compose = await mail.Mail.GetComposeAsync(mail.Priya.Id, item.ItemId, MailComposeMode.ReplyAll);

        Assert.Equal(new[] { mail.Ravi.Email, mail.Asha.Email }.Order(), compose!.To.Order());
        Assert.Equal([mail.Kiran.Email], compose.Cc);
    }

    [Fact]
    public async Task Reply_from_my_sent_mail_goes_back_to_its_recipients()
    {
        await using var mail = MailTestHarness.Create();
        var sent = await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya], cc: [mail.Kiran]));

        var reply = await mail.Mail.GetComposeAsync(mail.Ravi.Id, sent.ItemId, MailComposeMode.Reply);
        var replyAll = await mail.Mail.GetComposeAsync(mail.Ravi.Id, sent.ItemId, MailComposeMode.ReplyAll);

        Assert.Equal([mail.Priya.Email], reply!.To);
        Assert.Equal([mail.Priya.Email], replyAll!.To);
        Assert.Equal([mail.Kiran.Email], replyAll.Cc);
    }

    [Fact]
    public async Task Forward_has_no_recipients_quotes_the_original_and_carries_attachments()
    {
        await using var mail = MailTestHarness.Create();
        var files = new[] { AddUpload(mail, mail.Ravi, "a.pdf", 100), AddUpload(mail, mail.Ravi, "b.png", 200) };
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya], "Specs", "See attached") with { AttachmentIds = files.Select(file => file.Id).ToList() });
        var item = (await mail.FolderAsync(mail.Priya, MailView.Inbox)).Single();

        var compose = await mail.Mail.GetComposeAsync(mail.Priya.Id, item.ItemId, MailComposeMode.Forward);
        Assert.Empty(compose!.To);
        Assert.Equal("Fwd: Specs", compose.Subject);
        Assert.Contains("---------- Forwarded message ----------", compose.Body);
        Assert.Contains("See attached", compose.Body);
        Assert.Equal(["a.pdf", "b.png"], compose.ForwardedAttachments.Select(file => file.FileName));

        await mail.SendAsync(mail.Priya, new SendMailRequest([mail.Kiran.Email], null, compose.Subject, compose.Body, null, ParentItemId: item.ItemId, Mode: MailComposeMode.Forward));

        var forwarded = (await mail.FolderAsync(mail.Kiran, MailView.Inbox)).Single();
        var opened = await mail.Mail.OpenAsync(mail.Kiran.Id, forwarded.ItemId);
        Assert.True(forwarded.HasAttachments);
        Assert.Equal(["a.pdf", "b.png"], opened!.Message.Attachments.Select(file => file.FileName));
        var keys = await mail.Tracking.Db.MailAttachments.Select(file => file.StorageKey).Distinct().CountAsync();
        Assert.Equal(2, keys);
    }

    [Fact]
    public async Task Conversation_shows_my_mails_of_the_thread_oldest_first()
    {
        await using var mail = MailTestHarness.Create();
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya], "Kickoff", "1"));
        var first = (await mail.FolderAsync(mail.Priya, MailView.Inbox)).Single();
        await mail.SendAsync(mail.Priya, new SendMailRequest([mail.Ravi.Email], null, "Re: Kickoff", "2", null, ParentItemId: first.ItemId, Mode: MailComposeMode.Reply));
        var ravisReply = (await mail.FolderAsync(mail.Ravi, MailView.Inbox)).Single();
        // A forward to Kiran stays in the thread for Ravi and Priya only if it is in their mailbox.
        await mail.SendAsync(mail.Ravi, new SendMailRequest([mail.Kiran.Email], null, "Fwd: Kickoff", "3", null, ParentItemId: ravisReply.ItemId, Mode: MailComposeMode.Forward));

        var priyaView = await mail.Mail.OpenAsync(mail.Priya.Id, first.ItemId);
        Assert.Equal(["1", "2"], priyaView!.Thread.Select(message => message.Body));

        var raviView = await mail.Mail.OpenAsync(mail.Ravi.Id, ravisReply.ItemId);
        Assert.Equal(["1", "2", "3"], raviView!.Thread.Select(message => message.Body));

        var kiranItem = (await mail.FolderAsync(mail.Kiran, MailView.Inbox)).Single();
        var kiranView = await mail.Mail.OpenAsync(mail.Kiran.Id, kiranItem.ItemId);
        Assert.Equal(["3"], kiranView!.Thread.Select(message => message.Body));
    }

    [Fact]
    public async Task System_mail_cannot_be_replied_to_but_can_be_forwarded()
    {
        await using var mail = MailTestHarness.Create();
        var itemId = await AddSystemMailAsync(mail, mail.Priya);

        await Assert.ThrowsAsync<MailValidationException>(() => mail.Mail.GetComposeAsync(mail.Priya.Id, itemId, MailComposeMode.Reply));
        await Assert.ThrowsAsync<MailValidationException>(() => mail.Mail.GetComposeAsync(mail.Priya.Id, itemId, MailComposeMode.ReplyAll));
        await Assert.ThrowsAsync<MailValidationException>(() =>
            mail.Mail.SendAsync(mail.Priya.Id, MailTestHarness.To([mail.Ravi]) with { ParentItemId = itemId, Mode = MailComposeMode.Reply }));
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Inbox));

        var forward = await mail.Mail.GetComposeAsync(mail.Priya.Id, itemId, MailComposeMode.Forward);
        Assert.Contains("From: TaskFlow", forward!.Body);
        await mail.SendAsync(mail.Priya, MailTestHarness.To([mail.Ravi], forward.Subject, forward.Body) with { ParentItemId = itemId, Mode = MailComposeMode.Forward });
        Assert.Single(await mail.FolderAsync(mail.Ravi, MailView.Inbox));
    }

    internal static MailAttachment AddUpload(MailTestHarness mail, User owner, string name, long size)
    {
        var attachment = new MailAttachment
        {
            UploadedByUserId = owner.Id,
            FileName = name,
            ContentType = "application/octet-stream",
            SizeBytes = size,
            StorageKey = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        mail.Tracking.Db.MailAttachments.Add(attachment);
        mail.Tracking.Db.SaveChanges();
        return attachment;
    }

    private static async Task<int> AddSystemMailAsync(MailTestHarness mail, User recipient)
    {
        var message = new MailMessage
        {
            ConversationId = Guid.NewGuid(),
            IsSystem = true,
            Subject = "Task reminder: Report",
            Body = "Reminder",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SentAt = DateTime.UtcNow
        };
        mail.Tracking.Db.MailMessages.Add(message);
        await mail.Tracking.Db.SaveChangesAsync();
        mail.Tracking.Db.MailRecipients.Add(new MailRecipient { MessageId = message.Id, UserId = recipient.Id, Kind = MailRecipientKind.To });
        var item = new MailboxItem { MessageId = message.Id, OwnerUserId = recipient.Id, Folder = MailFolder.Inbox, SortAt = DateTime.UtcNow };
        mail.Tracking.Db.MailboxItems.Add(item);
        await mail.Tracking.Db.SaveChangesAsync();
        return item.Id;
    }
}
