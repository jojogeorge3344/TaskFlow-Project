using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests.Settings.TfaSettings;

/// <summary>The TFA Settings model list: "Use this model" (POST {kind}/models/use), Test connection for a listed model
/// (POST {kind}/test with a model name) and Edit (PUT {kind}/models?name=).</summary>
public class AiModelListEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Base = "/api/v1/assistant/providers";
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public AiModelListEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UsingAListedModelSwitchesTfaAndAProviderWithoutAKeyIsRefused()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "list-use-admin", SystemRoles.Administrator);
        await AddGeminiModelAsync(client, admin, "gemini-use-me");
        await ClearKeyAsync(AiApiType.OpenAI);

        var use = await SendAsync(client, HttpMethod.Post, $"{Base}/{_factory.ProviderId(AiApiType.Ollama)}/models/use", admin, new { modelName = "llama3.2:3b" });
        var noKey = await SendAsync(client, HttpMethod.Post, $"{Base}/{_factory.ProviderId(AiApiType.OpenAI)}/models/use", admin, new { modelName = "gpt-4o-mini" });
        var unknown = await SendAsync(client, HttpMethod.Post, $"{Base}/{_factory.ProviderId(AiApiType.Ollama)}/models/use", admin, new { modelName = "never-added" });

        Assert.Equal(HttpStatusCode.OK, use.StatusCode);
        using var providers = JsonDocument.Parse(await use.Content.ReadAsStringAsync());
        var ollama = providers.RootElement.EnumerateArray().Single(provider => provider.GetProperty("apiType").GetString() == "Ollama");
        Assert.True(ollama.GetProperty("isEnabled").GetBoolean());
        Assert.Equal("llama3.2:3b", ollama.GetProperty("modelName").GetString());
        var gemini = providers.RootElement.EnumerateArray().Single(provider => provider.GetProperty("apiType").GetString() == "Gemini");
        Assert.False(gemini.GetProperty("isEnabled").GetBoolean());
        Assert.True(gemini.GetProperty("keySet").GetBoolean());

        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Contains("No key saved for OpenAI GPT yet", await noKey.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task UsingAModelNeedsEditPermission()
    {
        var client = _factory.CreateClient();
        var role = await AddCustomRoleAsync("AI viewers", PermissionKeys.SettingsAiView);
        var viewer = await LoginInRoleAsync(client, "list-use-viewer", role.Id);

        var response = await SendAsync(client, HttpMethod.Post, $"{Base}/{_factory.ProviderId(AiApiType.Ollama)}/models/use", viewer, new { modelName = "llama3.2:3b" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestingAListedModelSendsThatModelAndChangesNothing()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "list-test-admin", SystemRoles.Administrator);
        await AddGeminiModelAsync(client, admin, "gemini-test-me");
        await SendAsync(client, HttpMethod.Post, $"{Base}/{_factory.ProviderId(AiApiType.Ollama)}/models/use", admin, new { modelName = "llama3.2:3b" });
        var before = await ReadConfigAsync(client, admin);
        var gemini = _factory.Providers[AiApiType.Gemini];
        AiProviderRuntime? sent = null;
        gemini.Handler = (runtime, _) =>
        {
            sent = runtime;
            return ChatModelResult.FromText("OK");
        };

        var response = await SendAsync(client, HttpMethod.Post, $"{Base}/{_factory.ProviderId(AiApiType.Gemini)}/test", admin, new { modelName = "gemini-test-me" });
        gemini.Reset();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(result.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("gemini-test-me", sent!.ModelName);
        Assert.Equal(before, await ReadConfigAsync(client, admin));
    }

    [Fact]
    public async Task EditingAnAddedModelRenamesItAndBuiltInOrInUseModelsAreRefused()
    {
        const string key = "AIzaSyEDITMODELSECRET-4455";
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "list-edit-admin", SystemRoles.Administrator);
        await AddGeminiModelAsync(client, admin, "gemini-edit-me");

        var inUse = await SendAsync(client, HttpMethod.Put, $"{Base}/{_factory.ProviderId(AiApiType.Gemini)}/models?name=gemini-edit-me", admin, new { modelName = "gemini-edited" });
        await SendAsync(client, HttpMethod.Post, $"{Base}/{_factory.ProviderId(AiApiType.Ollama)}/models/use", admin, new { modelName = "llama3.2:3b" });
        var builtIn = await SendAsync(client, HttpMethod.Put, $"{Base}/{_factory.ProviderId(AiApiType.Gemini)}/models?name=gemini-3.8-flash", admin, new { modelName = "gemini-3.9-flash" });
        var edit = await SendAsync(client, HttpMethod.Put, $"{Base}/{_factory.ProviderId(AiApiType.Gemini)}/models?name=gemini-edit-me", admin, new { modelName = "gemini-edited", apiKey = key });
        var body = await edit.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, inUse.StatusCode);
        Assert.Contains("is in use by TFA", await inUse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, builtIn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.DoesNotContain("EDITMODELSECRET", body);
        using var result = JsonDocument.Parse(body);
        Assert.True(result.RootElement.GetProperty("ok").GetBoolean());
        var gemini = result.RootElement.GetProperty("providers").EnumerateArray().Single(provider => provider.GetProperty("apiType").GetString() == "Gemini");
        var models = gemini.GetProperty("models").EnumerateArray().Select(model => model.GetString()).ToList();
        Assert.Contains("gemini-edited", models);
        Assert.DoesNotContain("gemini-edit-me", models);
        Assert.Equal("4455", gemini.GetProperty("keyLast4").GetString());
        Assert.False(gemini.GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public async Task EditingNeedsTestPermission()
    {
        var client = _factory.CreateClient();
        var role = await AddCustomRoleAsync("AI edit only", PermissionKeys.SettingsAiView, PermissionKeys.SettingsAiEdit);
        var editorOnly = await LoginInRoleAsync(client, "list-edit-editor", role.Id);

        var response = await SendAsync(client, HttpMethod.Put, $"{Base}/{_factory.ProviderId(AiApiType.Gemini)}/models?name=gemini-anything", editorOnly, new { modelName = "gemini-other" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Adds a Gemini model through the Add AI model endpoint, which also makes it the one TFA uses.</summary>
    private async Task AddGeminiModelAsync(HttpClient client, string token, string name)
    {
        var gemini = _factory.Providers[AiApiType.Gemini];
        gemini.Handler = (_, _) => ChatModelResult.FromText("OK");
        var response = await SendAsync(client, HttpMethod.Post, $"{Base}/models", token, new { providerId = _factory.ProviderId(AiApiType.Gemini), modelName = name, apiKey = "AIzaSyListTestKey-1234" });
        gemini.Reset();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task ClearKeyAsync(AiApiType kind)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var row in await db.AiProviderConfigs.Where(row => row.IsBuiltIn && row.ApiType == kind).ToListAsync())
        {
            row.EncryptedApiKey = null;
            row.KeyLast4 = null;
        }

        await db.SaveChangesAsync();
    }

    private async Task<string> ReadConfigAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}/config");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<Role> AddCustomRoleAsync(string name, params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var role = new Role { Name = $"{name} {Guid.NewGuid():N}"[..40], IsSystem = false, CreatedAt = DateTime.UtcNow };
        foreach (var permission in await db.Permissions.Where(permission => permissionKeys.Contains(permission.Key)).ToListAsync())
        {
            role.Permissions.Add(new RolePermission { Permission = permission });
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    private async Task<string> LoginInRoleAsync(HttpClient client, string username, int roleId)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(candidate => candidate.Username == username);
            user.RoleId = roleId;
            await db.SaveChangesAsync();
        }

        return await SignInAsync(client, username);
    }

    private async Task<string> LoginAsync(HttpClient client, string username, string role)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, role);
        return await SignInAsync(client, username);
    }

    private static async Task<string> SignInAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = Password });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string token, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
