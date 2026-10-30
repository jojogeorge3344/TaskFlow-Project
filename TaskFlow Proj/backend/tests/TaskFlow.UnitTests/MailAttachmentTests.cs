using TaskFlow.Api.Services.Communication.Mail.Attachments;
using TaskFlow.Api.Integrations.Storage;
using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Exceptions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

public sealed class MailAttachmentTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"taskflow-mail-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private MailAttachmentService Attachments(MailTestHarness mail) => TestServices.MailAttachments(mail.Tracking.Db, new LocalMailFileStore(_root), mail.Clock);

    private static MemoryStream Bytes(int count) => new(Enumerable.Repeat((byte)'x', count).ToArray());

    [Fact]
    public async Task Uploaded_file_attached_and_sent_can_be_downloaded_by_recipients()
    {
        await using var mail = MailTestHarness.Create();
        var upload = await Attachments(mail).UploadAsync(mail.Ravi.Id, new MemoryStream(Encoding.UTF8.GetBytes("quarterly numbers")), "report.pdf");

        Assert.Equal("report.pdf", upload.FileName);
        Assert.Equal("application/pdf", upload.ContentType);
        Assert.Equal(17, upload.SizeBytes);

        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya]) with { AttachmentIds = [upload.Id] });
        var item = (await mail.FolderAsync(mail.Priya, MailView.Inbox)).Single();
        var opened = await mail.Mail.OpenAsync(mail.Priya.Id, item.ItemId);
        Assert.Equal(["report.pdf"], opened!.Message.Attachments.Select(file => file.FileName));

        var download = await Attachments(mail).OpenAsync(mail.Priya.Id, upload.Id);
        Assert.NotNull(download);
        await using (download.Content)
        {
            Assert.Equal("quarterly numbers", await new StreamReader(download.Content).ReadToEndAsync());
        }

        Assert.Equal("report.pdf", download.FileName);
    }

    [Fact]
    public async Task Outsiders_cannot_download_and_unsent_uploads_are_private()
    {
        await using var mail = MailTestHarness.Create();
        var service = Attachments(mail);
        var pending = await service.UploadAsync(mail.Ravi.Id, Bytes(10), "notes.txt");
        var sent = await service.UploadAsync(mail.Ravi.Id, Bytes(10), "plan.txt");
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya]) with { AttachmentIds = [sent.Id] });

        Assert.Null(await service.OpenAsync(mail.Priya.Id, pending.Id));
        Assert.Null(await service.OpenAsync(mail.Kiran.Id, sent.Id));
        Assert.Null(await service.OpenAsync(mail.Asha.Id, sent.Id));
        (await service.OpenAsync(mail.Ravi.Id, pending.Id))!.Content.Dispose();
    }

    [Fact]
    public async Task Recipient_loses_access_after_deleting_the_mail_forever()
    {
        await using var mail = MailTestHarness.Create();
        var service = Attachments(mail);
        var file = await service.UploadAsync(mail.Ravi.Id, Bytes(5), "a.txt");
        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya]) with { AttachmentIds = [file.Id] });
        var item = (await mail.FolderAsync(mail.Priya, MailView.Inbox)).Single();

        await mail.Mail.TrashAsync(mail.Priya.Id, item.ItemId);
        (await service.OpenAsync(mail.Priya.Id, file.Id))!.Content.Dispose();
        await mail.Mail.DeleteForeverAsync(mail.Priya.Id, item.ItemId);

        Assert.Null(await service.OpenAsync(mail.Priya.Id, file.Id));
    }

    [Fact]
    public async Task Oversized_file_is_rejected_and_nothing_is_stored()
    {
        await using var mail = MailTestHarness.Create();

        var error = await Assert.ThrowsAsync<MailValidationException>(() =>
            Attachments(mail).UploadAsync(mail.Ravi.Id, Bytes((int)MailRules.MaxAttachmentBytes + 1), "big.pdf"));

        Assert.Contains("10 MB", error.Message);
        Assert.Equal(0, await mail.Tracking.Db.MailAttachments.CountAsync());
        Assert.Empty(Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : []);
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("run.PS1")]
    [InlineData("payload.js")]
    public async Task Blocked_types_are_rejected(string name)
    {
        await using var mail = MailTestHarness.Create();

        await Assert.ThrowsAsync<MailValidationException>(() => Attachments(mail).UploadAsync(mail.Ravi.Id, Bytes(10), name));

        Assert.Equal(0, await mail.Tracking.Db.MailAttachments.CountAsync());
    }

    [Fact]
    public async Task Empty_file_is_rejected()
    {
        await using var mail = MailTestHarness.Create();

        await Assert.ThrowsAsync<MailValidationException>(() => Attachments(mail).UploadAsync(mail.Ravi.Id, Bytes(0), "empty.txt"));
    }

    [Fact]
    public async Task Path_tricks_in_the_name_are_harmless()
    {
        await using var mail = MailTestHarness.Create();
        var service = Attachments(mail);

        var upload = await service.UploadAsync(mail.Ravi.Id, Bytes(3), "../../appsettings.json");

        Assert.Equal("appsettings.json", upload.FileName);
        Assert.Equal("application/octet-stream", upload.ContentType);
        var stored = Assert.Single(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
        Assert.StartsWith(Path.GetFullPath(_root), Path.GetFullPath(stored));
        Assert.DoesNotContain("appsettings", stored);
        var download = await service.OpenAsync(mail.Ravi.Id, upload.Id);
        download!.Content.Dispose();
        Assert.Equal("appsettings.json", download.FileName);
    }

    [Fact]
    public async Task Mail_limits_on_count_and_total_size_are_enforced()
    {
        await using var mail = MailTestHarness.Create();
        var six = Enumerable.Range(0, 6).Select(i => MailDraftAndThreadTests.AddUpload(mail, mail.Ravi, $"f{i}.txt", 10).Id).ToList();
        var big = Enumerable.Range(0, 3).Select(i => MailDraftAndThreadTests.AddUpload(mail, mail.Ravi, $"b{i}.pdf", 9L * 1024 * 1024).Id).ToList();

        await Assert.ThrowsAsync<MailValidationException>(() => mail.Mail.SendAsync(mail.Ravi.Id, MailTestHarness.To([mail.Priya]) with { AttachmentIds = six }));
        await Assert.ThrowsAsync<MailValidationException>(() => mail.Mail.SendAsync(mail.Ravi.Id, MailTestHarness.To([mail.Priya]) with { AttachmentIds = big }));
        Assert.Empty(await mail.FolderAsync(mail.Priya, MailView.Inbox));
        Assert.Equal(0, await mail.Tracking.Db.MailMessages.CountAsync());
    }

    [Fact]
    public async Task Someone_elses_upload_cannot_be_attached()
    {
        await using var mail = MailTestHarness.Create();
        var priyas = MailDraftAndThreadTests.AddUpload(mail, mail.Priya, "hers.txt", 10);

        await Assert.ThrowsAsync<MailValidationException>(() =>
            mail.Mail.SendAsync(mail.Ravi.Id, MailTestHarness.To([mail.Kiran]) with { AttachmentIds = [priyas.Id] }));
    }

    [Fact]
    public async Task Draft_keeps_attachments_and_removing_one_detaches_it()
    {
        await using var mail = MailTestHarness.Create();
        var service = Attachments(mail);
        var keep = await service.UploadAsync(mail.Ravi.Id, Bytes(4), "keep.txt");
        var drop = await service.UploadAsync(mail.Ravi.Id, Bytes(4), "drop.txt");
        var draft = await mail.Mail.CreateDraftAsync(mail.Ravi.Id, new SaveDraftRequest([mail.Priya.Email], null, "s", "b", [keep.Id, drop.Id]));

        await mail.Mail.UpdateDraftAsync(mail.Ravi.Id, draft!.Message.MessageId, new SaveDraftRequest([mail.Priya.Email], null, "s", "b", [keep.Id]));
        var sent = await mail.Mail.SendAsync(mail.Ravi.Id, new SendMailRequest(null, null, null, null, null, DraftId: draft.Message.MessageId));

        Assert.Equal(["keep.txt"], sent!.Message.Attachments.Select(file => file.FileName));
        Assert.True(await service.DeleteAsync(mail.Ravi.Id, drop.Id));
        Assert.False(await service.DeleteAsync(mail.Ravi.Id, keep.Id));
    }

    [Fact]
    public async Task Pending_uploads_are_capped_per_user_and_free_up_once_sent()
    {
        await using var mail = MailTestHarness.Create();
        var service = Attachments(mail);
        var uploads = new List<int>();
        for (var i = 0; i < MailRules.MaxPendingUploads; i++)
        {
            uploads.Add((await service.UploadAsync(mail.Ravi.Id, Bytes(10), $"f{i}.txt")).Id);
        }

        var error = await Assert.ThrowsAsync<MailValidationException>(() => service.UploadAsync(mail.Ravi.Id, Bytes(10), "one-more.txt"));
        Assert.Contains("waiting to be attached", error.Message);
        Assert.Equal(MailRules.MaxPendingUploads, await mail.Tracking.Db.MailAttachments.CountAsync());

        // Another user is not affected.
        await service.UploadAsync(mail.Priya.Id, Bytes(10), "hers.txt");

        await mail.SendAsync(mail.Ravi, MailTestHarness.To([mail.Priya]) with { AttachmentIds = uploads.Take(5).ToList() });
        await service.UploadAsync(mail.Ravi.Id, Bytes(10), "now-fits.txt");
    }

    [Fact]
    public async Task Pending_uploads_are_capped_by_total_size()
    {
        await using var mail = MailTestHarness.Create();
        for (var i = 0; i < 10; i++)
        {
            MailDraftAndThreadTests.AddUpload(mail, mail.Ravi, $"big{i}.pdf", 10L * 1024 * 1024);
        }

        var error = await Assert.ThrowsAsync<MailValidationException>(() => Attachments(mail).UploadAsync(mail.Ravi.Id, Bytes(10), "small.txt"));

        Assert.Contains("100 MB", error.Message);
    }

    [Fact]
    public async Task File_that_would_pass_the_pending_size_cap_is_rejected_clearly()
    {
        await using var mail = MailTestHarness.Create();
        for (var i = 0; i < 9; i++)
        {
            MailDraftAndThreadTests.AddUpload(mail, mail.Ravi, $"big{i}.pdf", 10L * 1024 * 1024);
        }

        MailDraftAndThreadTests.AddUpload(mail, mail.Ravi, "almost.pdf", 9L * 1024 * 1024);

        var error = await Assert.ThrowsAsync<MailValidationException>(() =>
            Attachments(mail).UploadAsync(mail.Ravi.Id, Bytes(2 * 1024 * 1024), "two-mb.pdf"));

        Assert.Contains("past 100 MB", error.Message);
        Assert.Empty(Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : []);
    }
}
