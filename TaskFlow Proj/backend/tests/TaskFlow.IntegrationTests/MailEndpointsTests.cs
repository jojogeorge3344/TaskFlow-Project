using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TaskFlow.Domain.Enums;

namespace TaskFlow.IntegrationTests;

public class MailEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Url = "/api/v1/mail";

    private readonly TaskFlowApiFactory _factory;

    public MailEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    private sealed record Session(HttpClient Client, string Token, string Email);

    [Fact]
    public async Task Employee_mails_a_manager_and_both_folders_update()
    {
        var employee = await LoginAsync("mail-emp-a", Role.Employee);
        var manager = await LoginAsync("mail-mgr-a", Role.Manager);

        var send = await SendAsync(employee, HttpMethod.Post, $"{Url}/send", Json(new { to = new[] { manager.Email }, subject = "Leave request", body = "Friday off?" }));
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);

        var sent = await ListAsync(employee, "Sent");
        Assert.Equal("Leave request", sent[0].GetProperty("subject").GetString());

        var inbox = await ListAsync(manager, "Inbox");
        Assert.Equal("Leave request", inbox[0].GetProperty("subject").GetString());
        Assert.False(inbox[0].GetProperty("isRead").GetBoolean());
        Assert.Equal("mail-emp-a full name", inbox[0].GetProperty("from").GetProperty("name").GetString());

        var unread = await GetJsonAsync(manager, $"{Url}/unread-count");
        Assert.Equal(1, unread.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task External_address_is_rejected_and_nothing_is_delivered()
    {
        var sender = await LoginAsync("mail-ext-sender", Role.Employee);
        var colleague = await LoginAsync("mail-ext-colleague", Role.Employee);

        var response = await SendAsync(sender, HttpMethod.Post, $"{Url}/send", Json(new { to = new[] { colleague.Email, "someone@gmail.com" }, subject = "Hi", body = "x" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("someone@gmail.com", document.RootElement.GetProperty("errors").GetProperty("To")[0].GetString());
        Assert.Empty(await ListAsync(colleague, "Inbox"));
    }

    [Fact]
    public async Task Another_users_mail_is_not_found_even_for_an_administrator()
    {
        var a = await LoginAsync("mail-priv-a", Role.Employee);
        var b = await LoginAsync("mail-priv-b", Role.Employee);
        var admin = await LoginAsync("mail-priv-admin", Role.Administrator);
        await SendAsync(a, HttpMethod.Post, $"{Url}/send", Json(new { to = new[] { b.Email }, subject = "Private", body = "x" }));
        var itemId = (await ListAsync(b, "Inbox"))[0].GetProperty("itemId").GetInt32();

        foreach (var outsider in new[] { a, admin })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(outsider, HttpMethod.Get, $"{Url}/items/{itemId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(outsider, HttpMethod.Post, $"{Url}/items/{itemId}/trash")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(outsider, HttpMethod.Patch, $"{Url}/items/{itemId}", Json(new { isStarred = true }))).StatusCode);
        }
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Url}/unread-count")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync($"{Url}/send", Json(new { to = new[] { "x@y.z" }, subject = "s" }))).StatusCode);
    }

    [Fact]
    public async Task Reply_all_addresses_everyone_but_me_and_threads_together()
    {
        var sender = await LoginAsync("mail-ra-sender", Role.Manager);
        var me = await LoginAsync("mail-ra-me", Role.Employee);
        var other = await LoginAsync("mail-ra-other", Role.Employee);
        var cc = await LoginAsync("mail-ra-cc", Role.Employee);
        await SendAsync(sender, HttpMethod.Post, $"{Url}/send", Json(new { to = new[] { me.Email, other.Email }, cc = new[] { cc.Email }, subject = "Kickoff", body = "Agenda" }));
        var itemId = (await ListAsync(me, "Inbox"))[0].GetProperty("itemId").GetInt32();

        var compose = await GetJsonAsync(me, $"{Url}/items/{itemId}/compose?mode=ReplyAll");
        var to = compose.GetProperty("to").EnumerateArray().Select(value => value.GetString()).Order().ToList();
        Assert.Equal(new[] { sender.Email, other.Email }.Order(), to);
        Assert.Equal([cc.Email], compose.GetProperty("cc").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("Re: Kickoff", compose.GetProperty("subject").GetString());

        var reply = await SendAsync(me, HttpMethod.Post, $"{Url}/send", Json(new
        {
            to,
            cc = new[] { cc.Email },
            subject = compose.GetProperty("subject").GetString(),
            body = "Sounds good",
            parentItemId = itemId,
            mode = "ReplyAll"
        }));
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);

        var senderItem = (await ListAsync(sender, "Inbox"))[0].GetProperty("itemId").GetInt32();
        var opened = await GetJsonAsync(sender, $"{Url}/items/{senderItem}");
        Assert.Equal(["Agenda", "Sounds good"], opened.GetProperty("thread").EnumerateArray().Select(message => message.GetProperty("body").GetString()));
    }

    [Fact]
    public async Task Draft_lifecycle_save_update_send()
    {
        var author = await LoginAsync("mail-draft-author", Role.Employee);
        var reader = await LoginAsync("mail-draft-reader", Role.Employee);

        var created = await SendAsync(author, HttpMethod.Post, $"{Url}/drafts", Json(new { to = new[] { "unfinished" }, subject = "Plan", body = "v1" }));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var messageId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("message").GetProperty("messageId").GetInt32();
        Assert.Empty(await ListAsync(reader, "Inbox"));

        var updated = await SendAsync(author, HttpMethod.Put, $"{Url}/drafts/{messageId}", Json(new { to = new[] { reader.Email }, subject = "Plan", body = "v2" }));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var sent = await SendAsync(author, HttpMethod.Post, $"{Url}/send", Json(new { draftId = messageId }));
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        Assert.Empty(await ListAsync(author, "Drafts"));
        Assert.Equal("v2", (await ListAsync(reader, "Inbox"))[0].GetProperty("preview").GetString());
    }

    [Fact]
    public async Task Trash_restore_delete_forever_star_and_search()
    {
        var sender = await LoginAsync("mail-ops-sender", Role.Employee);
        var me = await LoginAsync("mail-ops-me", Role.Employee);
        await SendAsync(sender, HttpMethod.Post, $"{Url}/send", Json(new { to = new[] { me.Email }, subject = "Invoice 42", body = "Pay soon" }));
        var itemId = (await ListAsync(me, "Inbox"))[0].GetProperty("itemId").GetInt32();

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(me, HttpMethod.Patch, $"{Url}/items/{itemId}", Json(new { isStarred = true }))).StatusCode);
        Assert.Single(await ListAsync(me, "Starred"));
        Assert.Single(await ListAsync(me, "Inbox", "invoice"));
        Assert.Empty(await ListAsync(me, "Inbox", "nothing-like-this"));

        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(me, HttpMethod.Delete, $"{Url}/items/{itemId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(me, HttpMethod.Post, $"{Url}/items/{itemId}/trash")).StatusCode);
        Assert.Empty(await ListAsync(me, "Inbox"));
        Assert.Single(await ListAsync(me, "Trash"));
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(me, HttpMethod.Post, $"{Url}/items/{itemId}/restore")).StatusCode);
        Assert.Single(await ListAsync(me, "Inbox"));

        await SendAsync(me, HttpMethod.Post, $"{Url}/items/{itemId}/trash");
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(me, HttpMethod.Delete, $"{Url}/items/{itemId}")).StatusCode);
        Assert.Empty(await ListAsync(me, "Trash"));
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(me, HttpMethod.Get, $"{Url}/items/{itemId}")).StatusCode);
        Assert.Single(await ListAsync(sender, "Sent"));
    }

    [Fact]
    public async Task Attachments_upload_download_and_access_rules()
    {
        var sender = await LoginAsync("mail-att-sender", Role.Employee);
        var receiver = await LoginAsync("mail-att-receiver", Role.Employee);
        var outsider = await LoginAsync("mail-att-outsider", Role.Employee);

        var upload = await UploadAsync(sender, "report.pdf", "PDF-CONTENT");
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var attachmentId = JsonDocument.Parse(await upload.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();

        var blocked = await UploadAsync(sender, "setup.exe", "MZ");
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);

        await SendAsync(sender, HttpMethod.Post, $"{Url}/send", Json(new { to = new[] { receiver.Email }, subject = "Report", body = "Attached", attachmentIds = new[] { attachmentId } }));
        Assert.True((await ListAsync(receiver, "Inbox"))[0].GetProperty("hasAttachments").GetBoolean());

        var download = await SendAsync(receiver, HttpMethod.Get, $"{Url}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("PDF-CONTENT", await download.Content.ReadAsStringAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("report.pdf", download.Content.Headers.ContentDisposition?.FileNameStar ?? download.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Contains("nosniff", download.Headers.GetValues("X-Content-Type-Options"));

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(outsider, HttpMethod.Get, $"{Url}/attachments/{attachmentId}")).StatusCode);
    }

    [Fact]
    public async Task Contacts_are_available_to_employees_and_list_only_active_users()
    {
        var employee = await LoginAsync("mail-contacts-emp", Role.Employee);
        await _factory.SeedUserAsync("mail-contacts-gone", "mail-contacts-gone@taskflow.local", "P@ssw0rd!", Role.Employee, isActive: false);
        await LoginAsync("mail-contacts-here", Role.Manager);

        var response = await SendAsync(employee, HttpMethod.Get, $"{Url}/contacts?search=mail-contacts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var emails = document.RootElement.EnumerateArray().Select(contact => contact.GetProperty("email").GetString()).ToList();
        Assert.Contains("mail-contacts-here@taskflow.local", emails);
        Assert.DoesNotContain("mail-contacts-gone@taskflow.local", emails);
    }

    private static JsonContent Json(object value) => JsonContent.Create(value);

    private async Task<HttpResponseMessage> UploadAsync(Session session, string fileName, string content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return await SendAsync(session, HttpMethod.Post, $"{Url}/attachments", form);
    }

    private async Task<IReadOnlyList<JsonElement>> ListAsync(Session session, string folder, string? search = null)
    {
        var url = $"{Url}?folder={folder}" + (search is null ? string.Empty : $"&search={Uri.EscapeDataString(search)}");
        var root = await GetJsonAsync(session, url);
        return root.GetProperty("items").EnumerateArray().Select(item => item.Clone()).ToList();
    }

    private async Task<JsonElement> GetJsonAsync(Session session, string url)
    {
        var response = await SendAsync(session, HttpMethod.Get, url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private async Task<Session> LoginAsync(string username, Role role)
    {
        const string password = "P@ssw0rd!";
        var email = $"{username}@taskflow.local";
        await _factory.SeedUserAsync(username, email, password, role);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return new Session(client, document.RootElement.GetProperty("accessToken").GetString()!, email);
    }

    private static async Task<HttpResponseMessage> SendAsync(Session session, HttpMethod method, string url, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        return await session.Client.SendAsync(request);
    }
}
