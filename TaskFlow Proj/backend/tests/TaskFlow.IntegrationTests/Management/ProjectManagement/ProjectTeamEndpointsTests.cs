using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.IntegrationTests.Management.ProjectManagement;

/// <summary>Project allocations end to end: the employee form and the project team write the same rows under the same 100% cap,
/// positions are a shared pick-or-type list, and a position grants nothing.</summary>
public class ProjectTeamEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Password = "P@ssw0rd!";
    private const int Member = 1;
    private const int Qa = 3;
    private const int Developer = 4;

    private readonly TaskFlowApiFactory _factory;

    public ProjectTeamEndpointsTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    // ------------------------------------------------------------------ positions

    [Fact]
    public async Task PositionsListTheSeededOnesAndTypingANewNameCreatesItOnceIgnoringCase()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-pos", SystemRoles.Administrator);

        using var list = await JsonAsync(client, "/api/v1/positions", admin);
        var names = list.RootElement.EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();
        Assert.Contains("QA", names);
        Assert.Contains("DevOps", names);

        var fresh = $"Scrum Master {Guid.NewGuid():N}"[..24];
        var created = await SendAsync(client, HttpMethod.Post, "/api/v1/positions", admin, new { name = fresh });
        var again = await SendAsync(client, HttpMethod.Post, "/api/v1/positions", admin, new { name = fresh.ToUpperInvariant() });
        var existing = await SendAsync(client, HttpMethod.Post, "/api/v1/positions", admin, new { name = "qa" });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var createdBody = await JsonAsync(created);
        using var againBody = await JsonAsync(again);
        using var existingBody = await JsonAsync(existing);
        Assert.Equal(createdBody.RootElement.GetProperty("id").GetInt32(), againBody.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(Qa, existingBody.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("QA", existingBody.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ABlankPositionNameIsABadRequest()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-blank", SystemRoles.Administrator);

        var response = await SendAsync(client, HttpMethod.Post, "/api/v1/positions", admin, new { name = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task APersonWithoutProjectPermissionsCannotCreatePositions()
    {
        var client = _factory.CreateClient();
        var employee = await SignInAsync(client, "team-employee-pos", SystemRoles.Employee);

        var response = await SendAsync(client, HttpMethod.Post, "/api/v1/positions", employee, new { name = "Hacker" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------ project team

    [Fact]
    public async Task AnAdministratorAssignsATeamAfterCreatingAProjectAndSeesItInTheMemberList()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-1", SystemRoles.Administrator);
        var tester = await SeedAsync("team-tester-1");
        var coder = await SeedManagerAsync("team-coder-1");
        var projectId = await CreateProjectAsync(client, admin, $"Team Apollo {Guid.NewGuid():N}");

        var saved = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{projectId}/members", admin, new
        {
            members = new object[]
            {
                new { userId = tester, positionId = Qa, percent = 60, isManager = false },
                new { userId = coder, positionId = Developer, percent = 100, isManager = true }
            }
        });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var members = await JsonAsync(client, $"/api/v1/projects/{projectId}/members", admin);
        var rows = members.RootElement.EnumerateArray()
            .Select(m => (m.GetProperty("fullName").GetString(), m.GetProperty("positionName").GetString(), m.GetProperty("percent").GetInt32(), m.GetProperty("isManager").GetBoolean()))
            .ToList();
        Assert.Equal([("team-coder-1 full name", "Developer", 100, true), ("team-tester-1 full name", "QA", 60, false)], rows);

        using var user = await JsonAsync(client, $"/api/v1/users/{tester}", admin);
        Assert.Equal(60, user.RootElement.GetProperty("totalAllocatedPercent").GetInt32());
        Assert.Equal(40, user.RootElement.GetProperty("remainingPercent").GetInt32());
        var allocation = Assert.Single(user.RootElement.GetProperty("projectAllocations").EnumerateArray());
        Assert.Equal("QA", allocation.GetProperty("positionName").GetString());
    }

    [Fact]
    public async Task ASecondProjectThatWouldPushSomeoneOverOneHundredIsRejectedWithTheirTotalAndNothingIsSaved()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-2", SystemRoles.Administrator);
        var person = await SeedManagerAsync("team-busy-2");
        var first = await CreateProjectAsync(client, admin, $"Cap First {Guid.NewGuid():N}");
        var second = await CreateProjectAsync(client, admin, $"Cap Second {Guid.NewGuid():N}");
        await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{first}/members", admin,
            new { members = new[] { new { userId = person, positionId = Member, percent = 70, isManager = true } } });

        var over = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{second}/members", admin,
            new { members = new[] { new { userId = person, positionId = Member, percent = 50, isManager = true } } });

        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        var message = await ErrorAsync(over);
        Assert.Contains("team-busy-2 full name", message);
        Assert.Contains("120%", message);
        Assert.Contains("30% remains", message);
        using var members = await JsonAsync(client, $"/api/v1/projects/{second}/members", admin);
        Assert.Empty(members.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task TheEmployeeFormAndTheProjectTeamShareTheSameCap()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-3", SystemRoles.Administrator);
        var person = await SeedManagerAsync("team-shared-3");
        var one = await CreateProjectAsync(client, admin, $"Shared One {Guid.NewGuid():N}");
        var two = await CreateProjectAsync(client, admin, $"Shared Two {Guid.NewGuid():N}");
        var current = await UserAsync(client, admin, person);

        var fromForm = await SendAsync(client, HttpMethod.Put, $"/api/v1/users/{person}", admin, new
        {
            employeeCode = (string?)null,
            fullName = current.GetProperty("fullName").GetString(),
            username = current.GetProperty("username").GetString(),
            email = current.GetProperty("email").GetString(),
            roleId = current.GetProperty("roleId").GetInt32(),
            projectAllocations = new[] { new { projectId = one, positionId = Qa, percent = 80 } }
        });
        var fromProject = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{two}/members", admin,
            new { members = new[] { new { userId = person, positionId = Member, percent = 30, isManager = true } } });

        Assert.Equal(HttpStatusCode.OK, fromForm.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fromProject.StatusCode);
        Assert.Contains("20% remains", await ErrorAsync(fromProject));
    }

    [Fact]
    public async Task CreatingAUserOverTheCapIs400AndCreatesNobody()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-4", SystemRoles.Administrator);
        var one = await CreateProjectAsync(client, admin, $"Create One {Guid.NewGuid():N}");
        var two = await CreateProjectAsync(client, admin, $"Create Two {Guid.NewGuid():N}");

        var response = await SendAsync(client, HttpMethod.Post, "/api/v1/users", admin, new
        {
            fullName = "Too Busy",
            username = "team-toobusy-4",
            email = "team-toobusy-4@taskflow.com",
            password = Password,
            roleId = SystemRoles.EmployeeId,
            isActive = true,
            projectAllocations = new[]
            {
                new { projectId = one, positionId = Member, percent = 70 },
                new { projectId = two, positionId = Member, percent = 40 }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("110%", await ErrorAsync(response));
        using var scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(u => u.Username == "team-toobusy-4"));
    }

    [Fact]
    public async Task AUserCreatedWithAllocationsListsThemAndTheProjectAndDepartmentFiltersFindThem()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-5", SystemRoles.Administrator);
        var one = await CreateProjectAsync(client, admin, $"Filter One {Guid.NewGuid():N}");
        var two = await CreateProjectAsync(client, admin, $"Filter Two {Guid.NewGuid():N}");
        var departmentId = await CreateDepartmentAsync($"Filter Dept {Guid.NewGuid():N}");

        var created = await SendAsync(client, HttpMethod.Post, "/api/v1/users", admin, new
        {
            fullName = "Many Hats",
            username = "team-manyhats-5",
            email = "team-manyhats-5@taskflow.com",
            password = Password,
            roleId = SystemRoles.EmployeeId,
            isActive = true,
            projectAllocations = new[]
            {
                new { projectId = one, positionId = Qa, percent = 50 },
                new { projectId = two, positionId = Developer, percent = 50 }
            },
            departmentIds = new[] { departmentId }
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var onTwo = await JsonAsync(client, $"/api/v1/users?projectId={two}", admin);
        using var inDepartment = await JsonAsync(client, $"/api/v1/users?departmentId={departmentId}", admin);
        Assert.Equal(["team-manyhats-5"], onTwo.RootElement.EnumerateArray().Select(u => u.GetProperty("username").GetString()));
        Assert.Equal(["team-manyhats-5"], inDepartment.RootElement.EnumerateArray().Select(u => u.GetProperty("username").GetString()));
        var departments = onTwo.RootElement[0].GetProperty("departments").EnumerateArray();
        Assert.Equal(departmentId, Assert.Single(departments).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task DeletingAProjectFreesTheAllocationsOfItsTeam()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-6", SystemRoles.Administrator);
        var person = await SeedManagerAsync("team-member-6");
        var projectId = await CreateProjectAsync(client, admin, $"Doomed {Guid.NewGuid():N}");
        await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{projectId}/members", admin,
            new { members = new[] { new { userId = person, positionId = Member, percent = 90, isManager = true } } });

        var deleted = await SendAsync(client, HttpMethod.Delete, $"/api/v1/projects/{projectId}", admin);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var user = await UserAsync(client, admin, person);
        Assert.Equal(100, user.GetProperty("remainingPercent").GetInt32());
        Assert.Empty(user.GetProperty("projectAllocations").EnumerateArray());
        using var scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().ManagerProjects.AnyAsync(l => l.ProjectId == projectId));
    }

    // ------------------------------------------------------------------ project managers

    [Fact]
    public async Task TheProjectEmployeeListNeverExposesCredentialsOrMfaState()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-leak", SystemRoles.Administrator);
        var person = await SeedManagerAsync("team-leak-m1");
        var projectId = await CreateProjectAsync(client, admin, $"No Leak {Guid.NewGuid():N}");
        var saved = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{projectId}/members", admin,
            new { members = new[] { new { userId = person, positionId = Member, percent = 40, isManager = true } } });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var employees = await JsonAsync(client, $"/api/v1/projects/{projectId}/employees", admin);

        var employee = Assert.Single(employees.RootElement.EnumerateArray());
        Assert.Equal(person, employee.GetProperty("id").GetInt32());
        foreach (var secret in new[] { "passwordHash", "securityStamp", "mfaSecretProtected", "mfaLastUsedStep", "refreshTokens", "mfaBackupCodes" })
        {
            Assert.False(employee.TryGetProperty(secret, out _), $"The employee list exposes {secret}.");
        }
    }

    [Fact]
    public async Task ATeamWithoutAManagerIsRejectedAndNothingIsSaved()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-m1", SystemRoles.Administrator);
        var person = await SeedManagerAsync("team-nomgr-m1");
        var projectId = await CreateProjectAsync(client, admin, $"No Manager {Guid.NewGuid():N}");

        var response = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{projectId}/members", admin,
            new { members = new[] { new { userId = person, positionId = Member, percent = 40, isManager = false } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("manager", (await ErrorAsync(response))!, StringComparison.OrdinalIgnoreCase);
        using var members = await JsonAsync(client, $"/api/v1/projects/{projectId}/members", admin);
        Assert.Empty(members.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task APersonWithoutTheManagerRoleCannotBeFlaggedAsManager()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-m2", SystemRoles.Administrator);
        var employee = await SeedAsync("team-emp-m2");
        var projectId = await CreateProjectAsync(client, admin, $"Wrong Role {Guid.NewGuid():N}");

        var response = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{projectId}/members", admin,
            new { members = new[] { new { userId = employee, positionId = Member, percent = 40, isManager = true } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("team-emp-m2 full name", (await ErrorAsync(response))!);
    }

    [Fact]
    public async Task FlaggedManagersAreListedOnTheProjectAndLinkedToIt()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-m3", SystemRoles.Administrator);
        var maya = await SeedManagerAsync("team-maya-m3");
        var ravi = await SeedManagerAsync("team-ravi-m3");
        var employee = await SeedAsync("team-emp-m3");
        var projectId = await CreateProjectAsync(client, admin, $"Two Managers {Guid.NewGuid():N}");

        var saved = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{projectId}/members", admin, new
        {
            members = new object[]
            {
                new { userId = maya, positionId = Member, percent = 20, isManager = true },
                new { userId = ravi, positionId = Member, percent = 20, isManager = true },
                new { userId = employee, positionId = Developer, percent = 60, isManager = false }
            }
        });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var projects = await JsonAsync(client, "/api/v1/projects", admin);
        var listed = projects.RootElement.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == projectId);
        Assert.Equal(
            ["team-maya-m3 full name", "team-ravi-m3 full name"],
            listed.GetProperty("managers").EnumerateArray().Select(m => m.GetString()));
        using var scope = _factory.Services.CreateScope();
        var linked = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ManagerProjects
            .Where(l => l.ProjectId == projectId).Select(l => l.ManagerUserId).OrderBy(id => id).ToListAsync();
        Assert.Equal(new[] { maya, ravi }.Order().ToList(), linked);
    }

    [Fact]
    public async Task APersonWhoCanOnlyViewProjectsCannotChangeATeam()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-7", SystemRoles.Administrator);
        var employee = await SignInAsync(client, "team-employee-7", SystemRoles.Employee);
        var person = await SeedAsync("team-member-7");
        var projectId = await CreateProjectAsync(client, admin, $"Locked {Guid.NewGuid():N}");

        var response = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{projectId}/members", employee,
            new { members = new[] { new { userId = person, positionId = Member, percent = 10 } } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task APositionLabelNamedManagerGivesNoManagerAccess()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-8", SystemRoles.Administrator);
        var lead = await SeedManagerAsync("team-lead-8");
        var person = await SeedAsync("team-labelled-8");
        var projectId = await CreateProjectAsync(client, admin, $"Label {Guid.NewGuid():N}");
        var saved = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{projectId}/members", admin, new
        {
            members = new object[]
            {
                new { userId = lead, positionId = Member, percent = 20, isManager = true },
                new { userId = person, positionId = 2, percent = 80, isManager = false }
            }
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var token = await TokenAsync(client, "team-labelled-8");
        var changeTeam = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{projectId}/members", token,
            new { members = new[] { new { userId = person, positionId = 2, percent = 80, isManager = true } } });

        Assert.Equal(HttpStatusCode.Forbidden, changeTeam.StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().ManagerProjects.AnyAsync(l => l.ManagerUserId == person));
    }

    [Fact]
    public async Task ASeveralPeopleTeamIsSavedTogetherAndUnknownProjectsAreNotFound()
    {
        var client = _factory.CreateClient();
        var admin = await SignInAsync(client, "team-admin-9", SystemRoles.Administrator);

        var response = await SendAsync(client, HttpMethod.Put, "/api/v1/projects/999999/members", admin, new { members = Array.Empty<object>() });
        var members = await SendAsync(client, HttpMethod.Get, "/api/v1/projects/999999/members", admin);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, members.StatusCode);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<int> SeedAsync(string username)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Employee);
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Username == username)).Id;
    }

    private async Task<int> SeedManagerAsync(string username)
    {
        await _factory.SeedUserAsync(username, $"{username}@taskflow.com", Password, SystemRoles.Manager);
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Username == username)).Id;
    }

    private async Task<int> CreateDepartmentAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var department = new TaskFlow.Api.Models.Entities.Department { Name = name, CreatedAt = DateTime.UtcNow };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        return department.Id;
    }

    private static async Task<int> CreateProjectAsync(HttpClient client, string token, string name)
    {
        var response = await SendAsync(client, HttpMethod.Post, "/api/v1/projects", token, new { name, description = (string?)null });
        response.EnsureSuccessStatusCode();
        using var body = await JsonAsync(response);
        return body.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> UserAsync(HttpClient client, string token, int id)
    {
        var response = await SendAsync(client, HttpMethod.Get, $"/api/v1/users/{id}", token);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
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
        return document.RootElement.GetProperty("detail").GetString();
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
}
