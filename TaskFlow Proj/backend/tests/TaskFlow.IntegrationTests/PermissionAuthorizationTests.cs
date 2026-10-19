using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Authorization;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Authentication;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests;

/// <summary>Access follows the permissions a role holds, read from the database on every request.</summary>
public class PermissionAuthorizationTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";

    private readonly TaskFlowApiFactory _factory;

    public PermissionAuthorizationTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    // ------------------------------------------------------------------ 4.1 policies

    [Fact]
    public async Task EveryCatalogPermissionHasAPolicyThatNeedsItsClaim()
    {
        using var scope = _factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        foreach (var permission in PermissionCatalog.All)
        {
            var policy = PermissionPolicy.NameFor(permission.Key);
            var holder = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthClaims.Permission, permission.Key)], "test"));
            var other = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthClaims.Permission, "something.else")], "test"));

            Assert.True((await authorization.AuthorizeAsync(holder, policy)).Succeeded, permission.Key);
            Assert.False((await authorization.AuthorizeAsync(other, policy)).Succeeded, permission.Key);
            Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity("test")), policy)).Succeeded, permission.Key);
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task AnAdministratorTokenCarriesNoPermissionsButRequestsGetThemFromTheDatabase()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "perm-admin-1", SystemRoles.Administrator);

        // The token itself holds only the identity; the permission claims are added when the request is validated.
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == AuthClaims.Permission);

        var response = await GetAsync(client, "/api/v1/dashboard/admin", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------ 4.2 profile payload

    [Theory]
    [InlineData(SystemRoles.Administrator)]
    [InlineData(SystemRoles.Manager)]
    [InlineData(SystemRoles.Employee)]
    public async Task TheProfileReturnsTheRoleNameAndEveryPermissionKeyTheRoleHolds(string role)
    {
        var client = _factory.CreateClient();
        var username = $"perm-me-{role.ToLowerInvariant()}";
        var token = await LoginAsync(client, username, role);

        var response = await GetAsync(client, "/api/v1/auth/me", token);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        var expected = PermissionCatalog.All
            .Where(permission => role == SystemRoles.Administrator
                || (role == SystemRoles.Manager && permission.GrantedToManager)
                || (role == SystemRoles.Employee && permission.GrantedToEmployee))
            .Select(permission => permission.Key)
            .Order()
            .ToList();
        Assert.Equal(role, body.RootElement.GetProperty("role").GetString());
        Assert.Equal(
            expected,
            body.RootElement.GetProperty("permissions").EnumerateArray().Select(key => key.GetString()!).Order().ToList());
    }

    [Fact]
    public async Task TheLoginResponseAlsoCarriesThePermissions()
    {
        await _factory.SeedUserAsync("perm-login", "perm-login@taskflow.local", Password, SystemRoles.Manager);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = "perm-login", password = Password });
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        var keys = body.RootElement.GetProperty("user").GetProperty("permissions").EnumerateArray().Select(key => key.GetString()).ToList();
        Assert.Contains(PermissionKeys.TasksCreate, keys);
        Assert.DoesNotContain(PermissionKeys.RolesManage, keys);
    }

    // ------------------------------------------------------------------ 4.5 no legacy role policies

    [Fact]
    public void EveryControllerActionUsesAPermissionPolicyTaskAccessOrNoRoleChecksAtAll()
    {
        var known = PermissionCatalog.All.Select(permission => PermissionPolicy.NameFor(permission.Key)).ToHashSet();
        var anonymousActions = new List<string>();
        var checkedActions = 0;

        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();

        Assert.NotEmpty(controllers);

        foreach (var controller in controllers)
        {
            var classAuthorize = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any()))
            {
                var name = $"{controller.Name}.{action.Name}";
                var authorize = classAuthorize.Concat(action.GetCustomAttributes<AuthorizeAttribute>(inherit: true)).ToList();
                var allowsAnonymous = action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
                    || controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

                if (allowsAnonymous)
                {
                    anonymousActions.Add(name);
                    continue;
                }

                Assert.True(authorize.Count > 0, $"{name} has no authorization at all.");
                foreach (var attribute in authorize)
                {
                    Assert.True(string.IsNullOrEmpty(attribute.Roles), $"{name} checks role names ({attribute.Roles}).");
                    Assert.True(
                        string.IsNullOrEmpty(attribute.Policy)
                        || known.Contains(attribute.Policy)
                        || attribute.Policy == AuthPolicies.TaskAccess,
                        $"{name} uses the legacy or unknown policy '{attribute.Policy}'.");
                }

                checkedActions++;
            }
        }

        Assert.True(checkedActions > 30, "Expected to check the whole API surface.");
        Assert.Equal(
            ["AuthController.Login", "AuthController.Logout", "AuthController.Refresh"],
            anonymousActions.Order().ToList());
    }

    // ------------------------------------------------------------------ 4.6 changes apply without signing in again

    [Fact]
    public async Task APermissionRemovedFromACustomRoleRefusesTheUsersVeryNextRequest()
    {
        var role = await AddCustomRoleAsync("Workload Viewers", PermissionKeys.WorkloadView);
        var client = _factory.CreateClient();
        var token = await LoginInRoleAsync(client, "perm-custom-1", role.Id);

        var before = await GetAsync(client, "/api/v1/workload", token);
        await SetGrantsAsync(role.Id);
        var after = await GetAsync(client, "/api/v1/workload", token);
        await SetGrantsAsync(role.Id, PermissionKeys.WorkloadView);
        var restored = await GetAsync(client, "/api/v1/workload", token);

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Fact]
    public async Task ACustomRoleOnlyOpensWhatItsTickedPermissionsAllow()
    {
        var role = await AddCustomRoleAsync("Reporters", PermissionKeys.ReportsView);
        var client = _factory.CreateClient();
        var token = await LoginInRoleAsync(client, "perm-custom-2", role.Id);

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, "/api/v1/reports", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(client, "/api/v1/workload", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(client, "/api/v1/dashboard/admin", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(client, "/api/v1/users", token)).StatusCode);
        // Open to every signed-in user: no permission needed.
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, "/api/v1/dashboard/employee", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, "/api/v1/auth/me", token)).StatusCode);
    }

    // ------------------------------------------------------------------ helpers

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

    private async Task SetGrantsAsync(int roleId, params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.RolePermissions.RemoveRange(db.RolePermissions.Where(grant => grant.RoleId == roleId));
        foreach (var permission in await db.Permissions.Where(permission => permissionKeys.Contains(permission.Key)).ToListAsync())
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id });
        }

        await db.SaveChangesAsync();
    }

    private async Task<string> LoginInRoleAsync(HttpClient client, string username, int roleId)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.local", Password, SystemRoles.Employee);

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
        await _factory.SeedUserAsync(username, $"{username}@taskflow.local", Password, role);
        return await SignInAsync(client, username);
    }

    private static async Task<string> SignInAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = Password });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
