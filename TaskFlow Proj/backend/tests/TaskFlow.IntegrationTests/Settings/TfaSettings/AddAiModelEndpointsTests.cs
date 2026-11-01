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

/// <summary>POST /assistant/providers/{kind}/models: the Add AI model page's test-then-save-and-activate.</summary>
public class AddAiModelEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Base = "/api/v1/assistant/providers";
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public AddAiModelEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task APassingTestSavesTheModelMakesItActiveAndNeverReturnsTheKey()
    {
        const string key = "AIzaSyADDMODELSECRET-7788";
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "add-model-admin", SystemRoles.Administrator);
        var gemini = _factory.Providers[AiProviderKind.Gemini];
        AiProviderRuntime? sent = null;
        gemini.Handler = (runtime, _) =>
        {
            sent = runtime;
            return ChatModelResult.FromText("OK");
        };

        var response = await SendAsync(client, $"{Base}/Gemini/models", admin, new { modelName = "gemini-2.5-flash-lite", apiKey = key });
        var body = await response.Content.ReadAsStringAsync();
        gemini.Reset();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("ADDMODELSECRET", body);
        Assert.DoesNotContain(_factory.Logs.Lines, line => line.Contains("ADDMODELSECRET"));
        Assert.Equal("gemini-2.5-flash-lite", sent!.ModelName);
        Assert.Equal(key, sent.ApiKey);

        using var result = JsonDocument.Parse(body);
        Assert.True(result.RootElement.GetProperty("ok").GetBoolean());
        var providers = result.RootElement.GetProperty("providers").EnumerateArray().ToList();
        var saved = providers.Single(provider => provider.GetProperty("kind").GetString() == "Gemini");
        Assert.True(saved.GetProperty("isEnabled").GetBoolean());
        Assert.Equal("gemini-2.5-flash-lite", saved.GetProperty("modelName").GetString());
        Assert.Equal("7788", saved.GetProperty("keyLast4").GetString());
        Assert.All(providers.Where(provider => provider.GetProperty("kind").GetString() != "Gemini"),
            provider => Assert.False(provider.GetProperty("isEnabled").GetBoolean()));
    }

    [Fact]
    public async Task AFailedTestReturnsTheReasonAndSavesNothing()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "add-model-admin-fail", SystemRoles.Administrator);
        var before = await ReadConfigAsync(client, admin);
        var openAi = _factory.Providers[AiProviderKind.OpenAI];
        openAi.Handler = (_, _) => throw new ProviderFailureException(AiProviderKind.OpenAI, ProviderFailureKind.InvalidKey);

        var response = await SendAsync(client, $"{Base}/OpenAI/models", admin, new { modelName = "gpt-typo", apiKey = "sk-wrong-0000" });
        openAi.Reset();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(result.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("InvalidKey", result.RootElement.GetProperty("failureKind").GetString());
        Assert.Equal(JsonValueKind.Null, result.RootElement.GetProperty("providers").ValueKind);
        Assert.Equal(before, await ReadConfigAsync(client, admin));
    }

    [Fact]
    public async Task MissingModelNameOrMissingKeyIsRefusedWith400()
    {
        var client = _factory.CreateClient();
        var admin = await LoginAsync(client, "add-model-admin-400", SystemRoles.Administrator);

        var noName = await SendAsync(client, $"{Base}/Gemini/models", admin, new { modelName = "", apiKey = "AIzaSyKey" });
        var noKey = await SendAsync(client, $"{Base}/OpenAI/models", admin, new { modelName = "gpt-4.1-mini" });
        var badAddress = await SendAsync(client, $"{Base}/Ollama/models", admin, new { modelName = "phi3:mini", baseUrl = "http://169.254.169.254" });

        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badAddress.StatusCode);
        Assert.DoesNotContain(_factory.Providers[AiProviderKind.OpenAI].Calls, call => call.Request.ModelName == "gpt-4.1-mini");
    }

    [Fact]
    public async Task EditWithoutTestOrAnEmployeeIsForbidden()
    {
        var client = _factory.CreateClient();
        var role = await AddCustomRoleAsync("AI editors", PermissionKeys.SettingsAiView, PermissionKeys.SettingsAiEdit);
        var editorOnly = await LoginInRoleAsync(client, "add-model-editor", role.Id);
        var employee = await LoginAsync(client, "add-model-employee", SystemRoles.Employee);

        var editor = await SendAsync(client, $"{Base}/Gemini/models", editorOnly, new { modelName = "gemini-2.5-pro", apiKey = "AIzaSyKey" });
        var plain = await SendAsync(client, $"{Base}/Gemini/models", employee, new { modelName = "gemini-2.5-pro", apiKey = "AIzaSyKey" });

        Assert.Equal(HttpStatusCode.Forbidden, editor.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, plain.StatusCode);
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

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string url, string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
