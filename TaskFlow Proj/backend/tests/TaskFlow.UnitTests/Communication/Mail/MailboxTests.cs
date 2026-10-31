using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests.Communication.Mail;

public class MailboxTests
{
    [Fact]
    public async Task Folders_hold_received_sent_and_flagged_mail()
    {
        await using var mail = MailTestHarness.Create();
        await mail.SendAsync(mail.Priya, MailTestHarness.To([mail.Ravi], "First"));
        await mail.SendAsync(mail.Kiran, MailTestHarness.To([mail.Ravi], "Second"));
        var sent = await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya], "Reply"));
        await mail.Mail.UpdateFlagsAsync(mail.Ravi.Id, sent.ItemId, new UpdateMailFlagsRequest(null, true, null));

        Assert.Equal(["Second", "First"], (await mail.FolderAsync(mail.Ravi, MailView.Inbox)).Select(item => item.Subject));
        Assert.Equal(["Reply"], (await mail.FolderAsync(mail.Ravi, MailView.Sent)).Select(item => item.Subject));
        Assert.Equal(["Reply"], (await mail.FolderAsync(mail.Ravi, MailView.Starred)).Select(item => item.Subject));
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Important));
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Trash));
    }

    [Fact]
    public async Task List_rows_are_newest_first_with_indicators_and_preview()
    {
        await using var mail = MailTestHarness.Create();
        await mail.SendAsync(mail.Priya, MailTestHarness.To([mail.Ravi], "Older", "old"));
        await mail.SendAsync(mail.Kiran, MailTestHarness.To([mail.Ravi], "Newer", "Line one\n\nline   two"));
        var newer = (await mail.FolderAsync(mail.Ravi, MailView.Inbox)).First();
        await mail.Mail.UpdateFlagsAsync(mail.Ravi.Id, newer.ItemId, new UpdateMailFlagsRequest(null, true, true));

        var rows = await mail.FolderAsync(mail.Ravi, MailView.Inbox);

        var first = rows.First();
        Assert.Equal("Newer", first.Subject);
        Assert.Equal("Line one line two", first.Preview);
        Assert.Equal(mail.Kiran.FullName, first.From.Name);
        Assert.True(first.IsStarred && first.IsImportant && !first.IsRead);
        Assert.Equal("Older", rows.Last().Subject);
    }

    [Fact]
    public async Task List_is_paged()
    {
        await using var mail = MailTestHarness.Create();
        for (var i = 0; i < 3; i++)
        {
            await mail.SendAsync(mail.Priya, MailTestHarness.To([mail.Ravi], $"Mail {i}"));
        }

        var page = await mail.Mail.ListAsync(mail.Ravi.Id, new MailListQuery(MailView.Inbox, null, Page: 2, PageSize: 2));

        Assert.Equal(3, page.Total);
        Assert.Equal(["Mail 0"], page.Items.Select(item => item.Subject));
    }

    [Fact]
    public async Task Read_state_is_per_recipient_and_counts_unread_inbox_mail()
    {
        await using var mail = MailTestHarness.Create();
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya, mail.Kiran]));
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya]));
        var priyaItem = (await mail.FolderAsync(mail.Priya, MailView.Inbox)).Last();

        var opened = await mail.Mail.OpenAsync(mail.Priya.Id, priyaItem.ItemId);

        Assert.True(opened!.IsRead);
        Assert.Equal(1, await mail.Mail.GetUnreadCountAsync(mail.Priya.Id));
        Assert.Equal(1, await mail.Mail.GetUnreadCountAsync(mail.Kiran.Id));
        Assert.Equal(0, await mail.Mail.GetUnreadCountAsync(mail.Ravi.Id));

        await mail.Mail.UpdateFlagsAsync(mail.Priya.Id, priyaItem.ItemId, new UpdateMailFlagsRequest(false, null, null));
        Assert.Equal(2, await mail.Mail.GetUnreadCountAsync(mail.Priya.Id));
        Assert.Equal(new MailSummaryDto(2, 0, 0, 0), await mail.Mail.GetSummaryAsync(mail.Priya.Id));
    }

    [Fact]
    public async Task Another_users_item_is_not_found_even_for_an_administrator()
    {
        await using var mail = MailTestHarness.Create();
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya]));
        var priyaItem = (await mail.FolderAsync(mail.Priya, MailView.Inbox)).Single();

        foreach (var outsider in new[] { mail.Kiran, mail.Asha })
        {
            Assert.Null(await mail.Mail.OpenAsync(outsider.Id, priyaItem.ItemId));
            Assert.False(await mail.Mail.UpdateFlagsAsync(outsider.Id, priyaItem.ItemId, new UpdateMailFlagsRequest(true, true, true)));
            Assert.False(await mail.Mail.TrashAsync(outsider.Id, priyaItem.ItemId));
            Assert.False(await mail.Mail.DeleteForeverAsync(outsider.Id, priyaItem.ItemId));
            Assert.Null(await mail.Mail.GetComposeAsync(outsider.Id, priyaItem.ItemId, MailComposeMode.Reply));
            Assert.Null(await mail.Mail.SendAsync(outsider.Id, MailTestHarness.To([mail.Ravi]) with { ParentItemId = priyaItem.ItemId, Mode = MailComposeMode.Reply }));
            Assert.Empty(await mail.FolderAsync(outsider, MailView.Inbox));
        }

        Assert.False((await mail.FolderAsync(mail.Priya, MailView.Inbox)).Single().IsRead);
    }

    [Fact]
    public async Task Trash_restore_and_delete_forever_affect_only_my_copy()
    {
        await using var mail = MailTestHarness.Create();
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya, mail.Kiran]));
        var item = (await mail.FolderAsync(mail.Priya, MailView.Inbox)).Single();
        await mail.Mail.UpdateFlagsAsync(mail.Priya.Id, item.ItemId, new UpdateMailFlagsRequest(true, true, null));

        await mail.Mail.TrashAsync(mail.Priya.Id, item.ItemId);
        Assert.Empty(await mail.FolderAsync(mail.Priya, MailView.Inbox));
        Assert.Empty(await mail.FolderAsync(mail.Priya, MailView.Starred));
        Assert.True(Assert.Single(await mail.FolderAsync(mail.Priya, MailView.Trash)).IsTrashed);
        Assert.Single(await mail.FolderAsync(mail.Kiran, MailView.Inbox));
        Assert.Single(await mail.FolderAsync(mail.Ravi, MailView.Sent));

        await mail.Mail.RestoreAsync(mail.Priya.Id, item.ItemId);
        var restored = Assert.Single(await mail.FolderAsync(mail.Priya, MailView.Inbox));
        Assert.True(restored.IsRead && restored.IsStarred);

        await Assert.ThrowsAsync<ConflictException>(() => mail.Mail.DeleteForeverAsync(mail.Priya.Id, item.ItemId));

        await mail.Mail.TrashAsync(mail.Priya.Id, item.ItemId);
        Assert.True(await mail.Mail.DeleteForeverAsync(mail.Priya.Id, item.ItemId));
        foreach (var view in Enum.GetValues<MailView>())
        {
            Assert.Empty(await mail.FolderAsync(mail.Priya, view));
        }

        Assert.Empty(await mail.FolderAsync(mail.Priya, MailView.Trash, "report"));
        Assert.Null(await mail.Mail.OpenAsync(mail.Priya.Id, item.ItemId));
        Assert.Single(await mail.FolderAsync(mail.Kiran, MailView.Inbox));
        Assert.Equal(1, await mail.Tracking.Db.MailMessages.CountAsync());
    }

    [Fact]
    public async Task Flags_are_personal()
    {
        await using var mail = MailTestHarness.Create();
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya]));
        var item = (await mail.FolderAsync(mail.Priya, MailView.Inbox)).Single();

        await mail.Mail.UpdateFlagsAsync(mail.Priya.Id, item.ItemId, new UpdateMailFlagsRequest(null, true, true));

        Assert.Single(await mail.FolderAsync(mail.Priya, MailView.Starred));
        Assert.Single(await mail.FolderAsync(mail.Priya, MailView.Important));
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Starred));
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Important));
    }

    [Fact]
    public async Task Search_matches_subject_body_and_people_within_my_mailbox_and_folder()
    {
        await using var mail = MailTestHarness.Create();
        await mail.SendAsync(mail.Priya, MailTestHarness.To([mail.Ravi], "Budget", "Numbers attached"));
        await mail.SendAsync(mail.Kiran, MailTestHarness.To([mail.Ravi], "Lunch", "Monthly REPORT is late"));
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Kiran], "Report draft", "see body"));
        await mail.SendAsync(mail.Kiran, MailTestHarness.To([mail.Asha], "Private", "report for Asha only"));

        Assert.Equal(["Budget"], (await mail.FolderAsync(mail.Ravi, MailView.Inbox, "priya")).Select(item => item.Subject));
        Assert.Equal(["Lunch"], (await mail.FolderAsync(mail.Ravi, MailView.Inbox, "report")).Select(item => item.Subject));
        Assert.Equal(["Report draft"], (await mail.FolderAsync(mail.Ravi, MailView.Sent, "report")).Select(item => item.Subject));
        Assert.Equal(["Report draft"], (await mail.FolderAsync(mail.Ravi, MailView.Sent, "kiran")).Select(item => item.Subject));
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Inbox, "%"));
        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Inbox, "_"));
        Assert.DoesNotContain(await mail.FolderAsync(mail.Ravi, MailView.Inbox, "asha only"), item => item.Subject == "Private");
    }

    [Fact]
    public async Task Search_does_not_look_in_trash_unless_inside_trash()
    {
        await using var mail = MailTestHarness.Create();
        await mail.SendAsync(mail.Priya, MailTestHarness.To([mail.Ravi], "Invoice"));
        var item = (await mail.FolderAsync(mail.Ravi, MailView.Inbox)).Single();
        await mail.Mail.TrashAsync(mail.Ravi.Id, item.ItemId);

        Assert.Empty(await mail.FolderAsync(mail.Ravi, MailView.Inbox, "invoice"));
        Assert.Single(await mail.FolderAsync(mail.Ravi, MailView.Trash, "invoice"));
    }

    [Fact]
    public async Task Contacts_lists_up_to_ten_active_users_matching_the_search()
    {
        await using var mail = MailTestHarness.Create();
        for (var i = 0; i < 12; i++)
        {
            mail.Tracking.AddUser($"team{i:00}");
        }

        mail.Priya.IsActive = false;
        await mail.Tracking.Db.SaveChangesAsync();

        Assert.Equal(MailRules.MaxContacts, (await mail.Mail.GetContactsAsync("team")).Count);
        Assert.Empty(await mail.Mail.GetContactsAsync("priya"));
        Assert.Empty(await mail.Mail.GetContactsAsync("  "));
        var ravi = Assert.Single(await mail.Mail.GetContactsAsync("RAVI@"));
        Assert.Equal(mail.Ravi.Email, ravi.Email);
    }
}
