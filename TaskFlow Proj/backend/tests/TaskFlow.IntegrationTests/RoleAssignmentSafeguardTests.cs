using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests;

/// <summary>Giving anyone the Administrator role needs role management as well as the assign roles permission, and an
/// employee update never changes a password.</summary>
public class RoleAssignmentSafeguardTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public RoleAssignmentSafeguardTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ARoleThatCanAssignRolesButNotManageThemCannotCreateAnAdministrator()
    {
        var client = _factory.CreateClient();
        var token = await SignInInCustomRoleAsync(client, "safeguard-assign",
            PermissionKeys.EmployeesView, PermissionKeys.EmployeesCreate, PermissionKeys.EmployeesRoleAssign);

        var admin = await CreateAsync(client, token, "safeguard-new-admin", SystemRoles.AdministratorId);
        var manager = await CreateAsync(client, token, "safeguard-new-manager", SystemRoles.ManagerId);

        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.Equal(HttpStatusCode.Created, manager.StatusCode);
        Assert.False(await UserExistsAsync("safeguard-new-admin"));
    }

    [Fact]
    public async Task AnAdministratorCanCreateAnAdministrator()
    {
        var client = _factory.CreateClient();
        await _factory.SeedUserAsync("safeguard-admin", "safeguard-admin@taskflow.com", Password, SystemRoles.Administrator);
        var token = await SignInAsync(client, "safeguard-admin");

        var response = await CreateAsync(client, token, "safeguard-second-admin", SystemRoles.AdministratorId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ARoleThatCanAssignRolesButNotManageThemCannotPromoteOrDemoteAnAdministrator()
    {
        var client = _factory.CreateClient();
        var token = await SignInInCustomRoleAsync(client, "safeguard-edit",
            PermissionKeys.EmployeesView, PermissionKeys.EmployeesEdit, PermissionKeys.EmployeesRoleAssign);
        await _factory.SeedUserAsync("safeguard-target", "safeguard-target@taskflow.com", Password, SystemRoles.Employee);
        await _factory.SeedUserAsync("safeguard-boss", "safeguard-boss@taskflow.com", Password, SystemRoles.Administrator);
        var targetId = await IdOfAsync("safeguard-target");
        var bossId = await IdOfAsync("safeguard-boss");

        var promote = await UpdateAsync(client, token, targetId, "safeguard-target", SystemRoles.AdministratorId);
        var demote = await UpdateAsync(client, token, bossId, "safeguard-boss", SystemRoles.EmployeeId);
        var ordinary = await UpdateAsync(client, token, targetId, "safeguard-target", SystemRoles.ManagerId);

        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, demote.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ordinary.StatusCode);
        Assert.Equal(SystemRoles.AdministratorId, await RoleIdOfAsync(bossId));
    }

    [Fact]
    public async Task AnEmployeeUpdateSavesTheOtherFieldsAndNeverChangesThePassword()
    {
        var client = _factory.CreateClient();
        await _factory.SeedUserAsync("safeguard-editor", "safeguard-editor@taskflow.com", Password, SystemRoles.Administrator);
        await _factory.SeedUserAsync("safeguard-pw", "safeguard-pw@taskflow.com", Password, SystemRoles.Employee);
        var token = await SignInAsync(client, "safeguard-editor");
        var id = await IdOfAsync("safeguard-pw");

        var response = await SendJsonAsync(client, "PUT", $"/api/v1/users/{id}", token, new
        {
            employeeCode = (string?)null,
            fullName = "Renamed Person",
            username = "safeguard-pw",
            email = "safeguard-pw@taskflow.com",
            roleId = SystemRoles.EmployeeId,
            password = "Another#Passw0rd"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Renamed Person", await FullNameOfAsync(id));
        var oldPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "safeguard-pw", password = Password });
        var newPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "safeguard-pw", password = "Another#Passw0rd" });
        Assert.Equal(HttpStatusCode.OK, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, newPassword.StatusCode);
    }

    [Fact]
    public async Task TheRoleListForTheEmployeeFormNeedsAssignRolesButNotRoleManagement()
    {
        var client = _factory.CreateClient();
        var assignOnly = await SignInInCustomRoleAsync(client, "safeguard-list-assign", PermissionKeys.EmployeesView, PermissionKeys.EmployeesRoleAssign);
        var viewOnly = await SignInInCustomRoleAsync(client, "safeguard-list-view", PermissionKeys.EmployeesView);
        await _factory.SeedUserAsync("safeguard-list-admin", "safeguard-list-admin@taskflow.com", Password, SystemRoles.Administrator);
        var admin = await SignInAsync(client, "safeguard-list-admin");

        var forAssigner = await SendJsonAsync(client, "GET", "/api/v1/users/roles", assignOnly, null);
        var forViewer = await SendJsonAsync(client, "GET", "/api/v1/users/roles", viewOnly, null);
        var forAdmin = await SendJsonAsync(client, "GET", "/api/v1/users/roles", admin, null);

        Assert.Equal(HttpStatusCode.OK, forAssigner.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forViewer.StatusCode);
        var assignerNames = (await forAssigner.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(role => role.GetProperty("name").GetString()).ToList();
        var adminNames = (await forAdmin.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(role => role.GetProperty("name").GetString()).ToList();
        Assert.Contains(SystemRoles.Manager, assignerNames);
        Assert.Contains(SystemRoles.Employee, assignerNames);
        Assert.DoesNotContain(SystemRoles.Administrator, assignerNames);
        Assert.Contains(SystemRoles.Administrator, adminNames);
        Assert.DoesNotContain("permissionKeys", await forAssigner.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------- helpers

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string token, string username, int roleId) =>
        SendJsonAsync(client, "POST", "/api/v1/users", token, new
        {
            employeeCode = (string?)null,
            fullName = $"{username} person",
            username,
            email = $"{username}@taskflow.com",
            password = "Str0ng#Passw0rd",
            roleId,
            isActive = true
        });

    private static Task<HttpResponseMessage> UpdateAsync(HttpClient client, string token, int id, string username, int roleId) =>
        SendJsonAsync(client, "PUT", $"/api/v1/users/{id}", token, new
        {
            employeeCode = (string?)null,
            fullName = $"{username} person",
            username,
            email = $"{username}@taskflow.com",
            roleId
        });

    private async Task<string> SignInInCustomRoleAsync(HttpClient client, string username, params string[] permissionKeys)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            var role = new Role { Name = $"{username} {Guid.NewGuid():N}"[..40], IsSystem = false, CreatedAt = DateTime.UtcNow };
            foreach (var permission in await db.Permissions.Where(permission => permissionKeys.Contains(permission.Key)).ToListAsync())
            {
                role.Permissions.Add(new RolePermission { Permission = permission });
            }

            db.Roles.Add(role);
            await db.SaveChangesAsync();

            await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
            var user = await db.Users.SingleAsync(candidate => candidate.Username == username);
            user.RoleId = role.Id;
            await db.SaveChangesAsync();
        }

        return await SignInAsync(client, username);
    }

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<bool> UserExistsAsync(string username) => DbAsync(db => db.Users.AnyAsync(user => user.Username == username));

    private Task<int> IdOfAsync(string username) => DbAsync(db => db.Users.Where(user => user.Username == username).Select(user => user.Id).SingleAsync());

    private Task<int> RoleIdOfAsync(int id) => DbAsync(db => db.Users.Where(user => user.Id == id).Select(user => user.RoleId).SingleAsync());

    private Task<string> FullNameOfAsync(int id) => DbAsync(db => db.Users.Where(user => user.Id == id).Select(user => user.FullName).SingleAsync());

    private static async Task<string> SignInAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = Password });
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(HttpClient client, string method, string url, string token, object? body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
