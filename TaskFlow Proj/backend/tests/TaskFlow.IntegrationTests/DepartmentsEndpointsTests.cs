using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests;

/// <summary>Departments group managers and employees; only Administrators change them, and membership is replaced wholesale.</summary>
public class DepartmentsEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";
    private const string Base = "/api/v1/departments";

    private readonly TaskFlowApiFactory _factory;

    public DepartmentsEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnAdministratorCreatesRenamesAndDeletesADepartment()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-crud", SystemRoles.Administrator);
        var name = UniqueName("Crud");

        var created = await SendAsync(client, HttpMethod.Post, Base, admin, new { name, description = "Original description" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        using var body = await JsonAsync(created);
        var id = body.RootElement.GetProperty("id").GetInt32();
        Assert.EndsWith($"/{id}", created.Headers.Location!.ToString());
        Assert.Equal(name, body.RootElement.GetProperty("name").GetString());
        Assert.Equal("Original description", body.RootElement.GetProperty("description").GetString());
        Assert.True(body.RootElement.GetProperty("createdAt").GetDateTime() > DateTime.UnixEpoch);

        using var list = await JsonAsync(client, Base, admin);
        var listed = list.RootElement.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
        Assert.Equal(name, listed.GetProperty("name").GetString());
        Assert.Equal(0, listed.GetProperty("managerCount").GetInt32());
        Assert.Equal(0, listed.GetProperty("employeeCount").GetInt32());

        var renamed = await SendAsync(client, HttpMethod.Put, $"{Base}/{id}", admin,
            new { name = $"{name} Renamed", description = "New description" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        using var renamedBody = await JsonAsync(renamed);
        Assert.Equal($"{name} Renamed", renamedBody.RootElement.GetProperty("name").GetString());
        Assert.Equal("New description", renamedBody.RootElement.GetProperty("description").GetString());

        var deleted = await SendAsync(client, HttpMethod.Delete, $"{Base}/{id}", admin);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var after = await JsonAsync(client, Base, admin);
        Assert.DoesNotContain(after.RootElement.EnumerateArray(), item => item.GetProperty("id").GetInt32() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, $"{Base}/{id}", admin)).StatusCode);
    }

    [Fact]
    public async Task DuplicateNameIsRejected()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-dup", SystemRoles.Administrator);
        var name = UniqueName("Dup");
        var target = UniqueName("Dup Target");

        var first = await SendAsync(client, HttpMethod.Post, Base, admin, new { name, description = (string?)null });
        var clash = await SendAsync(client, HttpMethod.Post, Base, admin, new { name = name.ToLowerInvariant(), description = (string?)null });
        var targetCreated = await SendAsync(client, HttpMethod.Post, Base, admin, new { name = target, description = (string?)null });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, targetCreated.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, clash.StatusCode);
        Assert.Equal("Department name already exists.", await ErrorAsync(clash));

        using var firstBody = await JsonAsync(first);
        var firstId = firstBody.RootElement.GetProperty("id").GetInt32();
        using var targetBody = await JsonAsync(targetCreated);

        var renameClash = await SendAsync(client, HttpMethod.Put, $"{Base}/{firstId}", admin,
            new { name = targetBody.RootElement.GetProperty("name").GetString()!.ToLowerInvariant(), description = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, renameClash.StatusCode);
        Assert.Equal("Department name already exists.", await ErrorAsync(renameClash));
    }

    [Fact]
    public async Task ManagerCanViewButNotMutate()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-view", SystemRoles.Administrator);
        var manager = await SignInAsync(client, "dept-test-mgr-view", SystemRoles.Manager);
        using var created = await CreateAsync(client, admin, UniqueName("View"));
        var id = created.RootElement.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, Base, manager)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, $"{Base}/{id}", manager)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, $"{Base}/{id}/members", manager)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Post, Base, manager,
            new { name = UniqueName("View Blocked"), description = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Put, $"{Base}/{id}", manager,
            new { name = UniqueName("View Renamed"), description = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", manager,
            new { managerIds = Array.Empty<int>(), employeeIds = Array.Empty<int>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Delete, $"{Base}/{id}", manager)).StatusCode);

        // The refused writes left the department as the administrator created it.
        using var stillThere = await JsonAsync(client, $"{Base}/{id}", admin);
        Assert.Equal(created.RootElement.GetProperty("name").GetString(), stillThere.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task EmployeeCannotUseDepartments()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-emp-block", SystemRoles.Administrator);
        var employee = await SignInAsync(client, "dept-test-emp-block", SystemRoles.Employee);
        using var created = await CreateAsync(client, admin, UniqueName("Emp Block"));
        var id = created.RootElement.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, Base, employee)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, $"{Base}/{id}", employee)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, $"{Base}/{id}/members", employee)).StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedGets401()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Base)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Base, new { name = "Nope", description = (string?)null })).StatusCode);
    }

    [Fact]
    public async Task MembersReplaceAssignsAndClears()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-members", SystemRoles.Administrator);
        var manager = await SeedAsync("dept-test-mgr-members", SystemRoles.Manager);
        var employee = await SeedAsync("dept-test-emp-members");
        using var created = await CreateAsync(client, admin, UniqueName("Members"));
        var id = created.RootElement.GetProperty("id").GetInt32();
        var name = created.RootElement.GetProperty("name").GetString();

        var replaced = await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = new[] { manager.Id }, employeeIds = new[] { employee.Id } });
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        using var replacedBody = await JsonAsync(replaced);
        Assert.Equal(id, replacedBody.RootElement.GetProperty("departmentId").GetInt32());
        Assert.Equal(manager.Id, OnlyId(replacedBody.RootElement.GetProperty("managers")));
        Assert.Equal(employee.Id, OnlyId(replacedBody.RootElement.GetProperty("employees")));

        using var members = await JsonAsync(client, $"{Base}/{id}/members", admin);
        Assert.Equal(manager.Id, OnlyId(members.RootElement.GetProperty("managers")));
        Assert.Equal(employee.Id, OnlyId(members.RootElement.GetProperty("employees")));

        using var assigned = await JsonAsync(client, $"/api/v1/users/{employee.Id}", admin);
        Assert.Equal(id, assigned.RootElement.GetProperty("departmentId").GetInt32());
        Assert.Equal(name, assigned.RootElement.GetProperty("departmentName").GetString());

        using var withMembers = await JsonAsync(client, Base, admin);
        var listed = withMembers.RootElement.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
        Assert.Equal(1, listed.GetProperty("managerCount").GetInt32());
        Assert.Equal(1, listed.GetProperty("employeeCount").GetInt32());

        var cleared = await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = Array.Empty<int>(), employeeIds = Array.Empty<int>() });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);

        using var empty = await JsonAsync(client, $"{Base}/{id}/members", admin);
        Assert.Empty(empty.RootElement.GetProperty("managers").EnumerateArray());
        Assert.Empty(empty.RootElement.GetProperty("employees").EnumerateArray());

        using var withoutMembers = await JsonAsync(client, Base, admin);
        var listedAfter = withoutMembers.RootElement.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
        Assert.Equal(0, listedAfter.GetProperty("managerCount").GetInt32());
        Assert.Equal(0, listedAfter.GetProperty("employeeCount").GetInt32());

        using var unassigned = await JsonAsync(client, $"/api/v1/users/{employee.Id}", admin);
        Assert.Equal(JsonValueKind.Null, unassigned.RootElement.GetProperty("departmentId").ValueKind);
    }

    [Fact]
    public async Task DeleteIsBlockedWhileMembersExist()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-del", SystemRoles.Administrator);
        var manager = await SeedAsync("dept-test-mgr-del", SystemRoles.Manager);
        var employee = await SeedAsync("dept-test-emp-del");
        using var created = await CreateAsync(client, admin, UniqueName("Delete Blocked"));
        var id = created.RootElement.GetProperty("id").GetInt32();

        await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = Array.Empty<int>(), employeeIds = new[] { employee.Id } });
        var withEmployee = await SendAsync(client, HttpMethod.Delete, $"{Base}/{id}", admin);
        Assert.Equal(HttpStatusCode.BadRequest, withEmployee.StatusCode);
        Assert.Contains("still has members", await withEmployee.Content.ReadAsStringAsync());

        await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = new[] { manager.Id }, employeeIds = Array.Empty<int>() });
        var withManager = await SendAsync(client, HttpMethod.Delete, $"{Base}/{id}", admin);
        Assert.Equal(HttpStatusCode.BadRequest, withManager.StatusCode);
        Assert.Contains("still has members", await withManager.Content.ReadAsStringAsync());

        await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = Array.Empty<int>(), employeeIds = Array.Empty<int>() });
        var afterClearing = await SendAsync(client, HttpMethod.Delete, $"{Base}/{id}", admin);
        Assert.Equal(HttpStatusCode.NoContent, afterClearing.StatusCode);
    }

    [Fact]
    public async Task InvalidManagerIdRejectsWholeReplace()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-bad-mgr", SystemRoles.Administrator);
        var manager = await SeedAsync("dept-test-mgr-bad", SystemRoles.Manager);
        var employee = await SeedAsync("dept-test-emp-bad");
        using var created = await CreateAsync(client, admin, UniqueName("Bad Manager"));
        var id = created.RootElement.GetProperty("id").GetInt32();

        var initial = await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = new[] { manager.Id }, employeeIds = new[] { employee.Id } });
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);

        var rejected = await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = new[] { employee.Id }, employeeIds = Array.Empty<int>() });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains($"User {employee.Id} is not an active manager.", await rejected.Content.ReadAsStringAsync());

        using var members = await JsonAsync(client, $"{Base}/{id}/members", admin);
        Assert.Equal(manager.Id, OnlyId(members.RootElement.GetProperty("managers")));
        Assert.Equal(employee.Id, OnlyId(members.RootElement.GetProperty("employees")));
    }

    [Fact]
    public async Task InactiveEmployeeIdIsRejected()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-inactive", SystemRoles.Administrator);
        var inactive = await SeedAsync("dept-test-emp-inactive", SystemRoles.Employee, isActive: false);
        using var created = await CreateAsync(client, admin, UniqueName("Inactive"));
        var id = created.RootElement.GetProperty("id").GetInt32();

        var rejected = await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = Array.Empty<int>(), employeeIds = new[] { inactive.Id } });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains($"User {inactive.Id} is not an active user.", await rejected.Content.ReadAsStringAsync());

        using var members = await JsonAsync(client, $"{Base}/{id}/members", admin);
        Assert.Empty(members.RootElement.GetProperty("employees").EnumerateArray());
    }

    [Fact]
    public async Task MissingDepartmentReturns404()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-missing", SystemRoles.Administrator);
        const int missing = 999999;

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, $"{Base}/{missing}", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, $"{Base}/{missing}/members", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Put, $"{Base}/{missing}/members", admin,
            new { managerIds = Array.Empty<int>(), employeeIds = Array.Empty<int>() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Put, $"{Base}/{missing}", admin,
            new { name = "Ghost", description = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Delete, $"{Base}/{missing}", admin)).StatusCode);
    }

    [Fact]
    public async Task ValidationRejectsBlankAndOverlongNames()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-validation", SystemRoles.Administrator);
        using var created = await CreateAsync(client, admin, UniqueName("Validation"));
        var id = created.RootElement.GetProperty("id").GetInt32();

        var blank = await SendAsync(client, HttpMethod.Post, Base, admin, new { name = "   ", description = (string?)null });
        var overlong = await SendAsync(client, HttpMethod.Post, Base, admin, new { name = new string('x', 201), description = (string?)null });
        var blankRename = await SendAsync(client, HttpMethod.Put, $"{Base}/{id}", admin, new { name = "", description = (string?)null });
        var overlongRename = await SendAsync(client, HttpMethod.Put, $"{Base}/{id}", admin, new { name = new string('y', 201), description = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overlong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blankRename.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overlongRename.StatusCode);
        Assert.False(await DbAsync(db => db.Departments.AnyAsync(department => department.Name == string.Empty)));
        using var unchanged = await JsonAsync(client, $"{Base}/{id}", admin);
        Assert.Equal(created.RootElement.GetProperty("name").GetString(), unchanged.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task DeletingAManagerRemovesTheirDepartmentLinkSoTheDepartmentCanBeDeleted()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-mgrdel", SystemRoles.Administrator);
        var manager = await SeedAsync("dept-test-mgr-mgrdel", SystemRoles.Manager);
        using var created = await CreateAsync(client, admin, UniqueName("Manager Deleted"));
        var id = created.RootElement.GetProperty("id").GetInt32();
        await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = new[] { manager.Id }, employeeIds = Array.Empty<int>() });

        var removed = await SendAsync(client, HttpMethod.Delete, $"/api/v1/users/{manager.Id}", admin);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        Assert.False(await DbAsync(db => db.DepartmentManagers.AnyAsync(link => link.ManagerUserId == manager.Id)));
        using var list = await JsonAsync(client, Base, admin);
        var listed = list.RootElement.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
        Assert.Equal(0, listed.GetProperty("managerCount").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Delete, $"{Base}/{id}", admin)).StatusCode);
    }

    [Fact]
    public async Task ALinkLeftByAnAlreadyDeletedManagerIsNeitherCountedNorBlocksDeletion()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-stale", SystemRoles.Administrator);
        var manager = await SeedAsync("dept-test-mgr-stale", SystemRoles.Manager);
        using var created = await CreateAsync(client, admin, UniqueName("Stale Link"));
        var id = created.RootElement.GetProperty("id").GetInt32();
        await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = new[] { manager.Id }, employeeIds = Array.Empty<int>() });

        // Rows written before managers' department links were cleaned up on delete: the user is gone, the link is not.
        await DbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE \"Users\" SET \"IsDeleted\" = 1 WHERE \"Id\" = {0}", manager.Id);
            return true;
        });

        using var list = await JsonAsync(client, Base, admin);
        var listed = list.RootElement.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
        Assert.Equal(0, listed.GetProperty("managerCount").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Delete, $"{Base}/{id}", admin)).StatusCode);
    }

    [Fact]
    public async Task ADeletedDepartmentsNameCanBeUsedAgain()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-reuse", SystemRoles.Administrator);
        var name = UniqueName("Reused");
        using var first = await CreateAsync(client, admin, name);
        var firstId = first.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Delete, $"{Base}/{firstId}", admin)).StatusCode);

        var again = await SendAsync(client, HttpMethod.Post, Base, admin, new { name, description = (string?)null });

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        using var secondBody = await JsonAsync(again);
        Assert.NotEqual(firstId, secondBody.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task OneReplaceCanAddAndRemoveEmployeesAtOnce()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "dept-test-admin-swap", SystemRoles.Administrator);
        var stays = await SeedAsync("dept-test-emp-swap-stays");
        var leaves = await SeedAsync("dept-test-emp-swap-leaves");
        var joins = await SeedAsync("dept-test-emp-swap-joins");
        using var created = await CreateAsync(client, admin, UniqueName("Swap"));
        var id = created.RootElement.GetProperty("id").GetInt32();
        await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = Array.Empty<int>(), employeeIds = new[] { stays.Id, leaves.Id } });

        var swapped = await SendAsync(client, HttpMethod.Put, $"{Base}/{id}/members", admin,
            new { managerIds = Array.Empty<int>(), employeeIds = new[] { stays.Id, joins.Id } });

        Assert.Equal(HttpStatusCode.OK, swapped.StatusCode);
        using var body = await JsonAsync(swapped);
        Assert.Equal(
            new[] { stays.Id, joins.Id }.Order(),
            body.RootElement.GetProperty("employees").EnumerateArray().Select(m => m.GetProperty("id").GetInt32()).Order());
        Assert.Null((await UserAsync("dept-test-emp-swap-leaves")).DepartmentId);
    }

    // ------------------------------------------------------------------ helpers

    private static string UniqueName(string label) => $"Dpt {label} {Guid.NewGuid():N}";

    private async Task<User> SeedAsync(string username, string role = SystemRoles.Employee, bool isActive = true)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, role, isActive);
        return await UserAsync(username);
    }

    private async Task<User> UserAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(user => user.Username == username);
    }

    private async Task<bool> DbAsync(Func<AppDbContext, Task<bool>> query)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    private async Task<string> SignInAsync(HttpClient client, string username, string role)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, role);
        return await TokenAsync(client, username);
    }

    private static async Task<string> TokenAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { emailOrUsername = username, password = Password });
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<JsonDocument> CreateAsync(HttpClient client, string token, string name)
    {
        var response = await SendAsync(client, HttpMethod.Post, Base, token, new { name, description = (string?)null });
        response.EnsureSuccessStatusCode();
        return await JsonAsync(response);
    }

    private static async Task<JsonDocument> JsonAsync(HttpClient client, string url, string token)
    {
        var response = await SendAsync(client, HttpMethod.Get, url, token);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    private static async Task<string?> ErrorAsync(HttpResponseMessage response)
    {
        using var document = await JsonAsync(response);
        return document.RootElement.GetProperty("error").GetString();
    }

    private static int OnlyId(JsonElement members) =>
        members.EnumerateArray().Select(member => member.GetProperty("id").GetInt32()).Single();

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
}
