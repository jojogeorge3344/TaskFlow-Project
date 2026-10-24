using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests;

/// <summary>Each protected action needs its own permission, checked on the server whatever the client shows.</summary>
public class ModuleBasedPermissionEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public ModuleBasedPermissionEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Method, url and the permissions that open it (more than one when a class-level key is also needed).</summary>
    public static TheoryData<string, string, string, string> GatedActions => new()
    {
        { "DELETE", "/api/v1/tasks/1", PermissionKeys.TasksDelete, "" },
        { "GET", "/api/v1/tasks/board", PermissionKeys.BoardView, "" },
        { "POST", "/api/v1/tasks/1/comments", PermissionKeys.TasksCommentCreate, "" },
        { "GET", "/api/v1/reports/export", PermissionKeys.ReportsExport, PermissionKeys.ReportsView },
        { "POST", "/api/v1/projects", PermissionKeys.ProjectsCreate, "" },
        { "PUT", "/api/v1/projects/1", PermissionKeys.ProjectsEdit, "" },
        { "DELETE", "/api/v1/projects/1", PermissionKeys.ProjectsDelete, "" },
        { "PUT", "/api/v1/projects/1/members", PermissionKeys.ProjectsMembers, "" },
        { "POST", "/api/v1/positions", PermissionKeys.PositionsCreate, "" },
        { "POST", "/api/v1/departments", PermissionKeys.DepartmentsCreate, "" },
        { "PUT", "/api/v1/departments/1", PermissionKeys.DepartmentsEdit, "" },
        { "PUT", "/api/v1/departments/1/members", PermissionKeys.DepartmentsMembers, "" },
        { "DELETE", "/api/v1/departments/1", PermissionKeys.DepartmentsDelete, "" },
        { "POST", "/api/v1/users", PermissionKeys.EmployeesCreate, "" },
        { "PUT", "/api/v1/users/999999", PermissionKeys.EmployeesEdit, "" },
        { "PATCH", "/api/v1/users/999999/status", PermissionKeys.EmployeesStatus, "" },
        { "DELETE", "/api/v1/users/999999", PermissionKeys.EmployeesDelete, "" },
        { "POST", "/api/v1/users/999999/mfa/reset", PermissionKeys.EmployeesMfaReset, "" },
        { "GET", "/api/v1/dashboard/admin", PermissionKeys.DashboardViewOrganization, "" },
        { "GET", "/api/v1/dashboard/manager", PermissionKeys.DashboardViewTeam, "" },
        { "GET", "/api/v1/dashboard/admin/analytics", PermissionKeys.DashboardAnalytics, PermissionKeys.DashboardViewOrganization },
        { "GET", "/api/v1/dashboard/admin/analytics/filters", PermissionKeys.DashboardFilters, PermissionKeys.DashboardViewOrganization },
        { "GET", "/api/v1/dashboard/manager/analytics", PermissionKeys.DashboardAnalytics, PermissionKeys.DashboardViewTeam },
        { "GET", "/api/v1/dashboard/employee/analytics", PermissionKeys.DashboardAnalytics, "" },
        { "GET", "/api/v1/assistant/providers/config", PermissionKeys.SettingsAiView, "" },
        { "PUT", "/api/v1/assistant/providers/config", PermissionKeys.SettingsAiEdit, "" },
        { "DELETE", "/api/v1/assistant/providers/OpenAi/models", PermissionKeys.SettingsAiRemoveModel, "" },
        { "POST", "/api/v1/assistant/providers/OpenAi/test", PermissionKeys.SettingsAiTest, "" },
        { "GET", "/api/v1/mail/summary", PermissionKeys.MailRead, "" },
        { "POST", "/api/v1/mail/send", PermissionKeys.MailCompose, "" },
        { "POST", "/api/v1/mail/items/1/trash", PermissionKeys.MailOrganize, "" },
        { "GET", "/api/v1/mail/attachments/1", PermissionKeys.MailAttachments, "" },
        { "GET", "/api/v1/notifications", PermissionKeys.NotificationsView, "" },
        { "POST", "/api/v1/notifications/read-all", PermissionKeys.NotificationsManage, "" },
        { "GET", "/api/v1/assistant/conversation", PermissionKeys.AssistantChat, "" },
        { "POST", "/api/v1/assistant/actions/00000000-0000-0000-0000-000000000001/confirm", PermissionKeys.AssistantActions, "" }
    };

    [Theory]
    [MemberData(nameof(GatedActions))]
    public async Task AnActionIsRefusedWithoutItsPermissionAndReachedWithIt(string method, string url, string key, string alsoNeeds)
    {
        var withoutKey = await AddCustomRoleAsync($"without {key}");
        var withKey = await AddCustomRoleAsync($"with {key}", [key, .. alsoNeeds.Length > 0 ? new[] { alsoNeeds } : []]);
        var client = _factory.CreateClient();
        var deniedToken = await LoginInRoleAsync(client, $"gate-no-{Guid.NewGuid():N}"[..24], withoutKey.Id);
        var allowedToken = await LoginInRoleAsync(client, $"gate-yes-{Guid.NewGuid():N}"[..24], withKey.Id);

        var denied = await SendAsync(client, method, url, deniedToken);
        var allowed = await SendAsync(client, method, url, allowedToken);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, allowed.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, allowed.StatusCode);
    }

    [Theory]
    [InlineData(nameof(TaskFlow.Api.Controllers.TasksController.ChangeStatus), PermissionKeys.TasksStatus)]
    [InlineData(nameof(TaskFlow.Api.Controllers.TasksController.GetActivity), PermissionKeys.TasksActivityView)]
    [InlineData(nameof(TaskFlow.Api.Controllers.TasksController.AddComment), PermissionKeys.TasksCommentCreate)]
    public void ActionsOnASingleTaskCarryTheirPermissionAlongsideTheTaskAccessRule(string action, string key)
    {
        // These also depend on whether the caller may see that task, so the permission is checked on the attribute.
        var method = typeof(TaskFlow.Api.Controllers.TasksController).GetMethod(action)!;

        Assert.Contains(
            method.GetCustomAttributes<AuthorizeAttribute>(),
            attribute => attribute.Policy == PermissionPolicy.Prefix + key);
    }

    [Fact]
    public async Task EveryGatedActionInTheTableUsesAKeyThatExistsInTheCatalogue()
    {
        var keys = PermissionCatalog.All.Select(definition => definition.Key).ToHashSet();

        foreach (var row in GatedActions)
        {
            Assert.Contains((string)row[2], keys);
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task TaskAndEmployeeDeletionNoLongerNeedRoleConfigurationAccess()
    {
        var onlyRoles = await AddCustomRoleAsync("Only roles", PermissionKeys.RolesManage);

        // Role Configuration access gives no other access: the deletes stay refused.
        var client = _factory.CreateClient();
        var token = await LoginInRoleAsync(client, $"gate-roles-{Guid.NewGuid():N}"[..24], onlyRoles.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, "DELETE", "/api/v1/tasks/1", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, "DELETE", "/api/v1/users/999999", token)).StatusCode);
    }

    [Fact]
    public async Task AHigherDashboardScopeIncludesTheLowerOne()
    {
        var organizationOnly = await AddCustomRoleAsync("Org only", PermissionKeys.DashboardViewOrganization);
        var teamOnly = await AddCustomRoleAsync("Team only", PermissionKeys.DashboardViewTeam);
        var client = _factory.CreateClient();
        var orgToken = await LoginInRoleAsync(client, $"scope-org-{Guid.NewGuid():N}"[..24], organizationOnly.Id);
        var teamToken = await LoginInRoleAsync(client, $"scope-team-{Guid.NewGuid():N}"[..24], teamOnly.Id);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, "GET", "/api/v1/dashboard/admin", orgToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, "GET", "/api/v1/dashboard/manager", orgToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, "GET", "/api/v1/dashboard/employee", orgToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, "GET", "/api/v1/dashboard/manager", teamToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, "GET", "/api/v1/dashboard/admin", teamToken)).StatusCode);
    }

    [Fact]
    public async Task ARoleStillHoldingAnOldBroadKeyKeepsItsOldAccess()
    {
        var legacy = await AddRoleWithRowsAsync("Old projects", [PermissionKeys.LegacyProjectsManage, PermissionKeys.ProjectsView]);
        var client = _factory.CreateClient();
        var token = await LoginInRoleAsync(client, $"legacy-{Guid.NewGuid():N}"[..24], legacy.Id);

        Assert.NotEqual(HttpStatusCode.Forbidden, (await SendAsync(client, "POST", "/api/v1/projects", token)).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await SendAsync(client, "DELETE", "/api/v1/projects/1", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, "POST", "/api/v1/departments", token)).StatusCode);
    }

    [Fact]
    public async Task OnlyKnownAreasAreOpenToEverySignedInUserWithoutAPermission()
    {
        var allowedPlainAuthorize = new[]
        {
            "ApplicationSettingsController", "WeatherForecastController", "AssistantController", "AuthController", "DashboardController", "MailController",
            "MfaController", "NotificationsController", "TasksController", "UsersController"
        };

        foreach (var controller in typeof(Program).Assembly.GetTypes()
                     .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract))
        {
            var classAuthorize = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any()))
            {
                if (action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
                {
                    continue;
                }

                var attributes = classAuthorize.Concat(action.GetCustomAttributes<AuthorizeAttribute>(inherit: true)).ToList();
                var needsPermission = attributes.Any(attribute => attribute.Policy?.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal) == true);

                if (!needsPermission)
                {
                    Assert.True(
                        allowedPlainAuthorize.Contains(controller.Name),
                        $"{controller.Name}.{action.Name} is open to every signed-in user but is not in a self-service area.");
                }
            }
        }

        await Task.CompletedTask;
    }

    // ------------------------------------------------------------------ roles API

    [Fact]
    public async Task TheCatalogueEndpointReturnsEveryEntryWithItsPlaceInTheHierarchy()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"cat-admin-{Guid.NewGuid():N}"[..24], SystemRoles.Administrator);

        var response = await SendAsync(client, "GET", "/api/v1/permissions", token);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var entries = body.RootElement.EnumerateArray().ToList();

        Assert.Equal(PermissionCatalog.All.Select(definition => definition.Key), entries.Select(entry => entry.GetProperty("key").GetString()!));
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrEmpty(entry.GetProperty("module").GetString()));
            Assert.False(string.IsNullOrEmpty(entry.GetProperty("screen").GetString()));
            Assert.False(string.IsNullOrEmpty(entry.GetProperty("action").GetString()));
        });
        Assert.All(
            entries.Where(entry => entry.GetProperty("alwaysGranted").GetBoolean()),
            entry => Assert.Equal(0, entry.GetProperty("id").GetInt32()));
        Assert.All(
            entries.Where(entry => !entry.GetProperty("alwaysGranted").GetBoolean()),
            entry => Assert.True(entry.GetProperty("id").GetInt32() > 0));
    }

    [Fact]
    public async Task SavingARoleNeedsTheViewPermissionItsActionsDependOn()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"req-admin-{Guid.NewGuid():N}"[..24], SystemRoles.Administrator);

        var missing = await SendJsonAsync(client, "POST", "/api/v1/roles", token, new { name = $"Dependent {Guid.NewGuid():N}"[..30], permissionKeys = new[] { PermissionKeys.ProjectsCreate } });
        var ok = await SendJsonAsync(client, "POST", "/api/v1/roles", token, new { name = $"Dependent ok {Guid.NewGuid():N}"[..30], permissionKeys = new[] { PermissionKeys.ProjectsCreate, PermissionKeys.ProjectsView } });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("View projects", await missing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    [Fact]
    public async Task AnOldKeyInASaveIsStoredAsItsFinerKeysAndAlwaysOnEntriesAreIgnored()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"old-admin-{Guid.NewGuid():N}"[..24], SystemRoles.Administrator);

        var response = await SendJsonAsync(client, "POST", "/api/v1/roles", token, new
        {
            name = $"Old style {Guid.NewGuid():N}"[..30],
            permissionKeys = new[] { PermissionKeys.LegacyDepartmentsManage, PermissionKeys.DepartmentsView, PermissionKeys.ProfileView }
        });
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var keys = body.RootElement.GetProperty("permissionKeys").EnumerateArray().Select(key => key.GetString()!).ToList();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(PermissionKeys.DepartmentsCreate, keys);
        Assert.Contains(PermissionKeys.DepartmentsDelete, keys);
        Assert.DoesNotContain(PermissionKeys.LegacyDepartmentsManage, keys);
        Assert.DoesNotContain(PermissionKeys.ProfileView, keys);
    }

    [Fact]
    public async Task ACopyOfASystemRoleCanBeEditedWithoutTouchingTheOriginal()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, $"copy-admin-{Guid.NewGuid():N}"[..24], SystemRoles.Administrator);
        var managerBefore = await KeysOfAsync(client, token, SystemRoles.ManagerId);

        var created = await SendJsonAsync(client, "POST", "/api/v1/roles", token, new { name = $"Manager copy {Guid.NewGuid():N}"[..30], permissionKeys = managerBefore });
        using var createdBody = await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync());
        var copyId = createdBody.RootElement.GetProperty("id").GetInt32();
        var trimmed = managerBefore.Where(key => key != PermissionKeys.TasksReassign).ToArray();
        var updated = await SendJsonAsync(client, "PUT", $"/api/v1/roles/{copyId}", token, new { name = $"Manager copy 2 {Guid.NewGuid():N}"[..30], permissionKeys = trimmed });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(managerBefore, await KeysOfAsync(client, token, SystemRoles.ManagerId));
        Assert.Contains(PermissionKeys.TasksReassign, managerBefore);
        Assert.DoesNotContain(PermissionKeys.TasksReassign, await KeysOfAsync(client, token, copyId));
    }

    // ------------------------------------------------------------------ helpers

    private static async Task<List<string>> KeysOfAsync(HttpClient client, string token, int roleId)
    {
        var response = await SendAsync(client, "GET", $"/api/v1/roles/{roleId}", token);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("permissionKeys").EnumerateArray().Select(key => key.GetString()!).ToList();
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

    /// <summary>Like <see cref="AddCustomRoleAsync"/>, but creates the old key rows when a fresh database does not have them.</summary>
    private async Task<Role> AddRoleWithRowsAsync(string name, string[] permissionKeys)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();

            foreach (var key in permissionKeys.Where(key => EffectivePermissions.IsLegacy(key)))
            {
                if (!await db.Permissions.AnyAsync(permission => permission.Key == key))
                {
                    db.Permissions.Add(new Permission { Key = key, Name = key, Description = key, Group = "Old" });
                }
            }

            await db.SaveChangesAsync();
        }

        return await AddCustomRoleAsync(name, permissionKeys);
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
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url, string token)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = JsonContent.Create(new { });
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(HttpClient client, string method, string url, string token, object body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
