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

namespace TaskFlow.IntegrationTests.Settings.RoleConfiguration;

public class RolesEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";
    private const string Roles = "/api/v1/roles";

    private readonly TaskFlowApiFactory _factory;

    public RolesEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    // ------------------------------------------------------------------ 5.1 endpoints and who may call them

    [Fact]
    public async Task AnAdministratorSeesTheSystemRolesWithTheirPermissionsAndTheCatalog()
    {
        var (client, token) = await SignInAsync("roles-admin-list", SystemRoles.Administrator);

        var roles = await JsonOf(await SendAsync(client, HttpMethod.Get, Roles, token));
        var catalog = await JsonOf(await SendAsync(client, HttpMethod.Get, "/api/v1/permissions", token));

        var byName = roles.EnumerateArray().ToDictionary(role => role.GetProperty("name").GetString()!);
        Assert.True(byName[SystemRoles.Administrator].GetProperty("isSystem").GetBoolean());
        Assert.True(byName[SystemRoles.Manager].GetProperty("isSystem").GetBoolean());
        Assert.True(byName[SystemRoles.Employee].GetProperty("isSystem").GetBoolean());
        Assert.Equal(PermissionCatalog.Stored.Count(), byName[SystemRoles.Administrator].GetProperty("permissionKeys").GetArrayLength());
        Assert.Equal(PermissionCatalog.Stored.Count(permission => permission.GrantedToEmployee), byName[SystemRoles.Employee].GetProperty("permissionKeys").GetArrayLength());
        Assert.Equal(
            PermissionCatalog.All.Select(permission => permission.Key),
            catalog.EnumerateArray().Select(permission => permission.GetProperty("key").GetString()!));
        Assert.True(catalog.EnumerateArray().Single(permission => permission.GetProperty("key").GetString() == PermissionKeys.RolesManage)
            .GetProperty("isAssignable").GetBoolean());
    }

    [Fact]
    public async Task AnAdministratorCanCreateReadUpdateAndDeleteACustomRole()
    {
        var (client, token) = await SignInAsync("roles-admin-crud", SystemRoles.Administrator);

        var created = await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "  Team Lead  ", permissionKeys = new[] { PermissionKeys.TasksCreate, PermissionKeys.ReportsView } });
        var createdBody = await JsonOf(created);
        var id = createdBody.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Team Lead", createdBody.GetProperty("name").GetString());
        Assert.False(createdBody.GetProperty("isSystem").GetBoolean());
        Assert.Equal([PermissionKeys.ReportsView, PermissionKeys.TasksCreate], Keys(createdBody));

        var updated = await JsonOf(await SendAsync(client, HttpMethod.Put, $"{Roles}/{id}", token, new { name = "Team Leader", permissionKeys = new[] { PermissionKeys.WorkloadView } }));
        Assert.Equal("Team Leader", updated.GetProperty("name").GetString());
        Assert.Equal([PermissionKeys.WorkloadView], Keys(updated));
        Assert.Equal([PermissionKeys.WorkloadView], Keys(await JsonOf(await SendAsync(client, HttpMethod.Get, $"{Roles}/{id}", token))));

        var deleted = await SendAsync(client, HttpMethod.Delete, $"{Roles}/{id}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, $"{Roles}/{id}", token)).StatusCode);
        Assert.False(await DbAsync(db => db.RolePermissions.AnyAsync(grant => grant.RoleId == id)));
    }

    [Theory]
    [InlineData(SystemRoles.Manager)]
    [InlineData(SystemRoles.Employee)]
    public async Task EveryRoleEndpointIsForbiddenToNonAdministrators(string role)
    {
        var (client, token) = await SignInAsync($"roles-denied-{role.ToLowerInvariant()}", role);

        await AssertAllForbiddenAsync(client, token);
    }

    [Fact]
    public async Task EveryRoleEndpointIsForbiddenToACustomRoleEvenWithManyPermissions()
    {
        var everythingAssignable = PermissionCatalog.All
            .Where(permission => permission.IsAssignable && permission.Key != PermissionKeys.RolesManage)
            .Select(permission => permission.Key)
            .ToArray();
        var roleId = await AddRoleAsync("Almost Admin", everythingAssignable);
        var (client, token) = await SignInInRoleAsync("roles-denied-custom", roleId);

        await AssertAllForbiddenAsync(client, token);
    }

    [Fact]
    public async Task RoleEndpointsRequireSignIn()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Roles)).StatusCode);
    }

    // ------------------------------------------------------------------ 5.2 rules

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SystemRolesRejectEveryChangeAndStayExactlyAsTheyWere(int systemRoleId)
    {
        var (client, token) = await SignInAsync($"roles-system-{systemRoleId}", SystemRoles.Administrator);
        var before = await JsonOf(await SendAsync(client, HttpMethod.Get, $"{Roles}/{systemRoleId}", token));

        var edit = await SendAsync(client, HttpMethod.Put, $"{Roles}/{systemRoleId}", token, new { name = "Renamed", permissionKeys = new[] { PermissionKeys.TasksCreate } });
        var empty = await SendAsync(client, HttpMethod.Put, $"{Roles}/{systemRoleId}", token, new { name = before.GetProperty("name").GetString(), permissionKeys = Array.Empty<string>() });
        var delete = await SendAsync(client, HttpMethod.Delete, $"{Roles}/{systemRoleId}", token);

        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Contains("read-only", (await JsonOf(edit)).GetProperty("detail").GetString());
        var after = await JsonOf(await SendAsync(client, HttpMethod.Get, $"{Roles}/{systemRoleId}", token));
        Assert.Equal(before.GetProperty("name").GetString(), after.GetProperty("name").GetString());
        Assert.Equal(Keys(before), Keys(after));
    }

    [Fact]
    public async Task ThePermissionCatalogCannotBeAddedToChangedOrDeletedByAnyone()
    {
        var (client, token) = await SignInAsync("roles-catalog", SystemRoles.Administrator);
        var before = await DbAsync(db => db.Permissions.AsNoTracking().OrderBy(permission => permission.Id).Select(permission => permission.Key + "|" + permission.Name).ToListAsync());

        var create = await SendAsync(client, HttpMethod.Post, "/api/v1/permissions", token, new { key = "invented.thing", name = "Invented" });
        var update = await SendAsync(client, HttpMethod.Put, "/api/v1/permissions/1", token, new { key = "x", name = "x" });
        var delete = await SendAsync(client, HttpMethod.Delete, "/api/v1/permissions/1", token);

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(before, await DbAsync(db => db.Permissions.AsNoTracking().OrderBy(permission => permission.Id).Select(permission => permission.Key + "|" + permission.Name).ToListAsync()));
    }

    [Fact]
    public async Task RoleManagementCanBeGivenToACustomRoleByTheAdministrator()
    {
        var (client, token) = await SignInAsync("roles-grant", SystemRoles.Administrator);

        var response = await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "Delegated Admin", permissionKeys = new[] { PermissionKeys.RolesManage } });
        var roleId = (await JsonOf(response)).GetProperty("id").GetInt32();
        var (delegated, delegatedToken) = await SignInInRoleAsync("roles-delegated", roleId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(delegated, HttpMethod.Get, Roles, delegatedToken)).StatusCode);
        var editSystem = await SendAsync(delegated, HttpMethod.Put, $"{Roles}/{SystemRoles.AdministratorId}", delegatedToken, new { name = "Hacked", permissionKeys = Array.Empty<string>() });
        Assert.NotEqual(HttpStatusCode.OK, editSystem.StatusCode);
        Assert.Equal(SystemRoles.Administrator, await DbAsync(db => db.Roles.Where(role => role.Id == SystemRoles.AdministratorId).Select(role => role.Name).SingleAsync()));
    }

    [Fact]
    public async Task ADelegatedRoleManagerCanManageOrdinaryRolesAndTickRoleConfigurationForThem()
    {
        var (admin, adminToken) = await SignInAsync("roles-deleg-admin", SystemRoles.Administrator);
        // Changing roles needs the modify-access key on top of roles.manage, which alone gives a read-only screen.
        var delegated = await SendAsync(admin, HttpMethod.Post, Roles, adminToken, new { name = "Role Managers", permissionKeys = new[] { PermissionKeys.RolesManage, PermissionKeys.RolesModifyAccess } });
        var (client, token) = await SignInInRoleAsync("roles-deleg-user", (await JsonOf(delegated)).GetProperty("id").GetInt32());

        var ordinary = await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "Ordinary By Delegate", permissionKeys = new[] { PermissionKeys.WorkloadView } });
        var ordinaryId = (await JsonOf(ordinary)).GetProperty("id").GetInt32();
        var rename = await SendAsync(client, HttpMethod.Put, $"{Roles}/{ordinaryId}", token, new { name = "Ordinary Renamed", permissionKeys = new[] { PermissionKeys.WorkloadView } });

        Assert.Equal(HttpStatusCode.Created, ordinary.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

        foreach (var key in new[] { PermissionKeys.EmployeesRoleAssign, PermissionKeys.RolesManage })
        {
            var keys = new[] { PermissionKeys.EmployeesView, key };
            var create = await SendAsync(client, HttpMethod.Post, Roles, token, new { name = $"Delegated {key}", permissionKeys = keys });
            var update = await SendAsync(client, HttpMethod.Put, $"{Roles}/{ordinaryId}", token, new { name = "Ordinary Renamed", permissionKeys = keys });
            var untick = await SendAsync(client, HttpMethod.Put, $"{Roles}/{ordinaryId}", token, new { name = "Ordinary Renamed", permissionKeys = new[] { PermissionKeys.WorkloadView } });

            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            Assert.Equal(HttpStatusCode.OK, untick.StatusCode);
        }

        var adminKey = await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "Wants Admin Role Key", permissionKeys = new[] { PermissionKeys.RolesAdministratorAssign } });
        Assert.Equal(HttpStatusCode.BadRequest, adminKey.StatusCode);
        Assert.Equal([PermissionKeys.WorkloadView], await DbAsync(db => db.RolePermissions.Where(grant => grant.RoleId == ordinaryId).Select(grant => grant.Permission.Key).ToListAsync()));
    }

    [Fact]
    public async Task ChangingARolesPermissionsSignsOutItsUsersButARenameDoesNot()
    {
        var (admin, adminToken) = await SignInAsync("roles-signout-admin", SystemRoles.Administrator);
        var created = await SendAsync(admin, HttpMethod.Post, Roles, adminToken, new { name = "Sign Out Role", permissionKeys = new[] { PermissionKeys.WorkloadView } });
        var roleId = (await JsonOf(created)).GetProperty("id").GetInt32();
        var (user, userToken) = await SignInInRoleAsync("roles-signout-user", roleId);

        await SendAsync(admin, HttpMethod.Put, $"{Roles}/{roleId}", adminToken, new { name = "Sign Out Role Renamed", permissionKeys = new[] { PermissionKeys.WorkloadView } });
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(user, HttpMethod.Get, "/api/v1/auth/me", userToken)).StatusCode);

        var change = await SendAsync(admin, HttpMethod.Put, $"{Roles}/{roleId}", adminToken, new { name = "Sign Out Role Renamed", permissionKeys = new[] { PermissionKeys.WorkloadView, PermissionKeys.ReportsView } });

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(user, HttpMethod.Get, "/api/v1/auth/me", userToken)).StatusCode);
    }

    [Fact]
    public async Task ARoleManagerWithoutModifyAccessCannotCreateEditOrDeleteRoles()
    {
        var (admin, adminToken) = await SignInAsync("roles-readonly-admin", SystemRoles.Administrator);
        var readOnly = await SendAsync(admin, HttpMethod.Post, Roles, adminToken, new { name = "Read Only Role Managers", permissionKeys = new[] { PermissionKeys.RolesManage } });
        var target = await SendAsync(admin, HttpMethod.Post, Roles, adminToken, new { name = "Read Only Target", permissionKeys = new[] { PermissionKeys.WorkloadView } });
        var targetId = (await JsonOf(target)).GetProperty("id").GetInt32();
        var (client, token) = await SignInInRoleAsync("roles-readonly-user", (await JsonOf(readOnly)).GetProperty("id").GetInt32());

        var create = await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "Not Allowed", permissionKeys = new[] { PermissionKeys.WorkloadView } });
        var update = await SendAsync(client, HttpMethod.Put, $"{Roles}/{targetId}", token, new { name = "Renamed", permissionKeys = new[] { PermissionKeys.WorkloadView } });
        var delete = await SendAsync(client, HttpMethod.Delete, $"{Roles}/{targetId}", token);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, Roles, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal("Read Only Target", await DbAsync(db => db.Roles.Where(role => role.Id == targetId).Select(role => role.Name).SingleAsync()));
    }

    [Fact]
    public async Task ADelegatedRoleManagerCanEditARoleThatHoldsAssignRoles()
    {
        var (admin, adminToken) = await SignInAsync("roles-restr-admin", SystemRoles.Administrator);
        var managers = await SendAsync(admin, HttpMethod.Post, Roles, adminToken, new { name = "Role Managers 2", permissionKeys = new[] { PermissionKeys.RolesManage, PermissionKeys.RolesModifyAccess } });
        var assigners = await SendAsync(admin, HttpMethod.Post, Roles, adminToken, new { name = "Assigners", permissionKeys = new[] { PermissionKeys.EmployeesView, PermissionKeys.EmployeesRoleAssign } });
        var assignersId = (await JsonOf(assigners)).GetProperty("id").GetInt32();
        var (client, token) = await SignInInRoleAsync("roles-restr-user", (await JsonOf(managers)).GetProperty("id").GetInt32());

        var edit = await SendAsync(client, HttpMethod.Put, $"{Roles}/{assignersId}", token, new { name = "Assigners", permissionKeys = new[] { PermissionKeys.EmployeesView } });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.DoesNotContain(PermissionKeys.EmployeesRoleAssign, await DbAsync(db => db.RolePermissions.Where(grant => grant.RoleId == assignersId).Select(grant => grant.Permission.Key).ToListAsync()));
    }

    [Fact]
    public async Task TheAdministratorOnlyKeyCannotBeGivenToAnyRole()
    {
        var (client, token) = await SignInAsync("roles-admin-key", SystemRoles.Administrator);

        var response = await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "Wants Admin Key", permissionKeys = new[] { PermissionKeys.RolesAdministratorAssign } });
        var catalog = await JsonOf(await SendAsync(client, HttpMethod.Get, "/api/v1/permissions", token));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(catalog.EnumerateArray().Single(permission => permission.GetProperty("key").GetString() == PermissionKeys.RolesAdministratorAssign).GetProperty("isAssignable").GetBoolean());
        Assert.Equal([SystemRoles.AdministratorId], await DbAsync(db => db.RolePermissions.Where(grant => grant.Permission.Key == PermissionKeys.RolesAdministratorAssign).Select(grant => grant.RoleId).ToListAsync()));
    }

    [Fact]
    public async Task ACustomRoleNameThatAlreadyExistsIsRejectedIgnoringCase()
    {
        var (client, token) = await SignInAsync("roles-dupe-custom", SystemRoles.Administrator);
        var first = await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "Duplicate Check", permissionKeys = Array.Empty<string>() });

        var second = await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "duplicate CHECK", permissionKeys = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, await DbAsync(db => db.Roles.CountAsync(role => role.Name == "Duplicate Check")));
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("manager")]
    [InlineData("ADMINISTRATOR")]
    [InlineData("employee")]
    public async Task ASystemRoleNameCannotBeReused(string name)
    {
        var (client, token) = await SignInAsync("roles-dupe-system", SystemRoles.Administrator);

        var response = await SendAsync(client, HttpMethod.Post, Roles, token, new { name, permissionKeys = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RenamingToAnotherRolesNameIsRejectedButKeepingYourOwnNameIsFine()
    {
        var (client, token) = await SignInAsync("roles-rename", SystemRoles.Administrator);
        var first = (await JsonOf(await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "Rename First", permissionKeys = Array.Empty<string>() }))).GetProperty("id").GetInt32();
        await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "Rename Second", permissionKeys = Array.Empty<string>() });

        var clash = await SendAsync(client, HttpMethod.Put, $"{Roles}/{first}", token, new { name = "rename second", permissionKeys = Array.Empty<string>() });
        var same = await SendAsync(client, HttpMethod.Put, $"{Roles}/{first}", token, new { name = "RENAME FIRST", permissionKeys = new[] { PermissionKeys.WorkloadView } });

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankRoleNameIsRejected(string name)
    {
        var (client, token) = await SignInAsync("roles-blank", SystemRoles.Administrator);

        var response = await SendAsync(client, HttpMethod.Post, Roles, token, new { name, permissionKeys = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownPermissionIsRejectedAndNothingIsSaved()
    {
        var (client, token) = await SignInAsync("roles-unknown", SystemRoles.Administrator);

        var response = await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "Unknown Perm Role", permissionKeys = new[] { PermissionKeys.TasksCreate, "does.not.exist" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await DbAsync(db => db.Roles.AnyAsync(role => role.Name == "Unknown Perm Role")));
    }

    [Fact]
    public async Task ARoleThatUsersHoldCannotBeDeletedAndTheMessageSaysHowMany()
    {
        var (client, token) = await SignInAsync("roles-inuse", SystemRoles.Administrator);
        var roleId = await AddRoleAsync("In Use Role");
        await SignInInRoleAsync("roles-inuse-a", roleId);
        await SignInInRoleAsync("roles-inuse-b", roleId);

        var response = await SendAsync(client, HttpMethod.Delete, $"{Roles}/{roleId}", token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("2 users", (await JsonOf(response)).GetProperty("detail").GetString());
        Assert.True(await DbAsync(db => db.Roles.AnyAsync(role => role.Id == roleId)));
        var listed = (await JsonOf(await SendAsync(client, HttpMethod.Get, Roles, token))).EnumerateArray().Single(role => role.GetProperty("id").GetInt32() == roleId);
        Assert.Equal(2, listed.GetProperty("userCount").GetInt32());
    }

    [Fact]
    public async Task EditingARolesPermissionsEndsItsUsersSessions()
    {
        var (client, token) = await SignInAsync("roles-live-admin", SystemRoles.Administrator);
        var roleId = await AddRoleAsync("Live Role", PermissionKeys.WorkloadView);
        var (userClient, userToken) = await SignInInRoleAsync("roles-live-user", roleId);

        var allowed = await SendAsync(userClient, HttpMethod.Get, "/api/v1/workload", userToken);
        await SendAsync(client, HttpMethod.Put, $"{Roles}/{roleId}", token, new { name = "Live Role", permissionKeys = new[] { PermissionKeys.ReportsView } });
        var signedOut = await SendAsync(userClient, HttpMethod.Get, "/api/v1/workload", userToken);
        var signedOutReports = await SendAsync(userClient, HttpMethod.Get, "/api/v1/reports", userToken);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, signedOutReports.StatusCode);
    }

    // ------------------------------------------------------------------ 5.3 assigning a role to a user

    [Fact]
    public async Task AnAdministratorCanMoveAUserToACustomRoleAndTheyUseItsPermissions()
    {
        var (client, token) = await SignInAsync("roles-assign-admin", SystemRoles.Administrator);
        await _factory.SeedUserAsync("roles-assign-target", "roles-assign-target@taskflow.com", Password, SystemRoles.Employee);
        var targetId = await DbAsync(db => db.Users.Where(user => user.Username == "roles-assign-target").Select(user => user.Id).SingleAsync());
        var roleId = await AddRoleAsync("Assignable Role", PermissionKeys.WorkloadView);

        var response = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{targetId}", token, UserBody("roles-assign-target", roleId));
        var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(roleId, body.GetProperty("roleId").GetInt32());
        Assert.Equal("Assignable Role", body.GetProperty("role").GetString());
        var targetToken = await LoginAsync(_factory.CreateClient(), "roles-assign-target");
        var me = await JsonOf(await SendAsync(_factory.CreateClient(), HttpMethod.Get, "/api/v1/auth/me", targetToken));
        Assert.Equal("Assignable Role", me.GetProperty("role").GetString());
        Assert.Equal([PermissionKeys.WorkloadView], me.GetProperty("permissions").EnumerateArray().Select(key => key.GetString()!).ToList());
    }

    [Fact]
    public async Task AnUnknownRoleIdIsRejectedWhenAssigningARole()
    {
        var (client, token) = await SignInAsync("roles-assign-bad", SystemRoles.Administrator);
        await _factory.SeedUserAsync("roles-assign-bad-t", "roles-assign-bad-t@taskflow.com", Password, SystemRoles.Employee);
        var targetId = await DbAsync(db => db.Users.Where(user => user.Username == "roles-assign-bad-t").Select(user => user.Id).SingleAsync());

        var response = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{targetId}", token, UserBody("roles-assign-bad-t", 99999));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OnlyRoleManagersMayChangeARoleEvenWhenTheyMayManageEmployees()
    {
        var peopleRoleId = await AddRoleAsync("People Managers", PermissionKeys.EmployeesCreate, PermissionKeys.EmployeesEdit, PermissionKeys.EmployeesView);
        var (client, token) = await SignInInRoleAsync("roles-people-mgr", peopleRoleId);
        await _factory.SeedUserAsync("roles-people-target", "roles-people-target@taskflow.com", Password, SystemRoles.Employee);
        var targetId = await DbAsync(db => db.Users.Where(user => user.Username == "roles-people-target").Select(user => user.Id).SingleAsync());

        var promote = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{targetId}", token, UserBody("roles-people-target", SystemRoles.ManagerId));
        var sameRole = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{targetId}", token, UserBody("roles-people-target", SystemRoles.EmployeeId, fullName: "Renamed By People Manager"));
        var createManager = await SendAsync(client, HttpMethod.Post, "/api/v1/users", token, CreateBody("roles-people-new-mgr", SystemRoles.ManagerId));
        var createEmployee = await SendAsync(client, HttpMethod.Post, "/api/v1/users", token, CreateBody("roles-people-new-emp", SystemRoles.EmployeeId));

        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sameRole.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createManager.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createEmployee.StatusCode);
        Assert.Equal(SystemRoles.EmployeeId, await DbAsync(db => db.Users.Where(user => user.Id == targetId).Select(user => user.RoleId).SingleAsync()));
    }

    // ------------------------------------------------------------------ helpers

    private async Task AssertAllForbiddenAsync(HttpClient client, string token)
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, Roles, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, $"{Roles}/1", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Post, Roles, token, new { name = "Nope", permissionKeys = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Put, $"{Roles}/4", token, new { name = "Nope", permissionKeys = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Delete, $"{Roles}/4", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, "/api/v1/permissions", token)).StatusCode);
        Assert.False(await DbAsync(db => db.Roles.AnyAsync(role => role.Name == "Nope")));
    }

    private static List<string> Keys(JsonElement role) =>
        role.GetProperty("permissionKeys").EnumerateArray().Select(key => key.GetString()!).Order().ToList();

    private static object UserBody(string username, int roleId, string fullName = "Updated Name") => new
    {
        employeeCode = (string?)null,
        fullName,
        username,
        email = $"{username}@taskflow.com",
        roleId,
        projectId = (int?)null,
        password = (string?)null
    };

    private static object CreateBody(string username, int roleId) => new
    {
        employeeCode = (string?)null,
        fullName = "New Person",
        username,
        email = $"{username}@taskflow.com",
        password = Password,
        roleId,
        isActive = true,
        projectId = (int?)null
    };

    private async Task<int> AddRoleAsync(string name, params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var role = new Role { Name = name, IsSystem = false, CreatedAt = DateTime.UtcNow };
        foreach (var permission in await db.Permissions.Where(permission => permissionKeys.Contains(permission.Key)).ToListAsync())
        {
            role.Permissions.Add(new RolePermission { Permission = permission });
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<(HttpClient Client, string Token)> SignInAsync(string username, string role)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, role);
        var client = _factory.CreateClient();
        return (client, await LoginAsync(client, username));
    }

    private async Task<(HttpClient Client, string Token)> SignInInRoleAsync(string username, int roleId)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        await DbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(candidate => candidate.Username == username);
            user.RoleId = roleId;
            await db.SaveChangesAsync();
            return 0;
        });

        var client = _factory.CreateClient();
        return (client, await LoginAsync(client, username));
    }

    private static async Task<string> LoginAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = Password });
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }
}
