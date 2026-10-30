using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Projects;
using TaskFlow.Application.Users;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

/// <summary>The rules for who may be on which project: whole percentages 1 to 100, one row per project, never more than 100% in
/// total for one person, from the employee form and from the project side alike.</summary>
public class UserAllocationServiceTests : IAsyncDisposable
{
    private const int Member = 1;
    private const int Qa = 3;
    private const int Developer = 4;

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly UserAllocationService _allocations;
    private readonly UserService _users;
    private readonly ProjectService _projects;
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();
    private int _counter;

    public UserAllocationServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        TestAccessControl.Seed(_db);
        _allocations = new UserAllocationService(_db);
        _users = new UserService(_db, _hasher, _allocations);
        _projects = new ProjectService(_db, _allocations);
    }

    private User AddManager(string? name = null, bool active = true) => AddUser(name, active, SystemRoles.ManagerId);

    private User AddUser(string? name = null, bool active = true, int roleId = SystemRoles.EmployeeId)
    {
        var username = name ?? $"alloc{Interlocked.Increment(ref _counter)}";
        var user = new User
        {
            FullName = $"{username} full name",
            Username = username,
            Email = $"{username}@taskflow.com",
            RoleId = roleId,
            IsActive = active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SecurityStamp = "stamp"
        };
        user.PasswordHash = _hasher.HashPassword(user, "P@ssw0rd!");
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private Project AddProject(string? name = null)
    {
        var project = new Project { Name = name ?? $"Project {Interlocked.Increment(ref _counter)}", CreatedAt = DateTime.UtcNow };
        _db.Projects.Add(project);
        _db.SaveChanges();
        return project;
    }

    private Task<UserDto?> UpdateAsync(User user, params ProjectAllocationInput[] allocations) =>
        _users.UpdateAsync(
            user.Id,
            new UpdateUserRequest(null, user.FullName, user.Username, user.Email, user.RoleId, allocations),
            default);

    private static ProjectAllocationInput Row(Project project, int percent, int position = Member) => new(project.Id, position, percent);

    private async Task<int> TotalAsync(User user) =>
        (await _db.UserProjectAllocations.Where(a => a.UserId == user.Id).ToListAsync()).Sum(a => a.AllocationPercent);

    // ------------------------------------------------------------------ many projects with a percentage

    [Fact]
    public async Task AUserCanBeOnTwoProjectsWithTheirOwnPercentagesAndPositions()
    {
        var user = AddUser();
        var apollo = AddProject("Apollo");
        var zephyr = AddProject("Zephyr");

        var dto = await UpdateAsync(user, Row(apollo, 60, Qa), Row(zephyr, 40, Developer));

        Assert.Equal(["Apollo", "Zephyr"], dto!.ProjectAllocations.Select(a => a.ProjectName));
        Assert.Equal([60, 40], dto.ProjectAllocations.Select(a => a.Percent));
        Assert.Equal(["QA", "Developer"], dto.ProjectAllocations.Select(a => a.PositionName));
        Assert.Equal(100, dto.TotalAllocatedPercent);
        Assert.Equal(0, dto.RemainingPercent);
    }

    [Fact]
    public async Task ExactlyOneHundredPercentIsAccepted()
    {
        var user = AddUser();

        var dto = await UpdateAsync(user, Row(AddProject(), 100));

        Assert.Equal(100, dto!.TotalAllocatedPercent);
    }

    [Fact]
    public async Task AUserWithNoProjectsTotalsZeroAndHasAHundredRemaining()
    {
        var dto = await _users.GetByIdAsync(AddUser().Id, default);

        Assert.Empty(dto!.ProjectAllocations);
        Assert.Equal(0, dto.TotalAllocatedPercent);
        Assert.Equal(100, dto.RemainingPercent);
    }

    [Fact]
    public async Task ReplacingTheSetChangesAddsAndRemovesRows()
    {
        var user = AddUser();
        var a = AddProject();
        var b = AddProject();
        var c = AddProject();
        await UpdateAsync(user, Row(a, 50), Row(b, 50));

        var dto = await UpdateAsync(user, Row(b, 30, Qa), Row(c, 70));

        Assert.Equal([b.Id, c.Id], dto!.ProjectAllocations.Select(x => x.ProjectId).Order());
        Assert.Equal(30, dto.ProjectAllocations.Single(x => x.ProjectId == b.Id).Percent);
        Assert.Equal("QA", dto.ProjectAllocations.Single(x => x.ProjectId == b.Id).PositionName);
    }

    [Fact]
    public async Task AnUpdateThatOmitsAllocationsLeavesThemAndAnEmptyListClearsThem()
    {
        var user = AddUser();
        var project = AddProject();
        await UpdateAsync(user, Row(project, 80));

        var untouched = await _users.UpdateAsync(
            user.Id, new UpdateUserRequest(null, "Renamed", user.Username, user.Email, user.RoleId), default);
        Assert.Equal(80, untouched!.TotalAllocatedPercent);

        var cleared = await UpdateAsync(user);
        Assert.Empty(cleared!.ProjectAllocations);
    }

    // ------------------------------------------------------------------ rejected input

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public async Task APercentageOutsideOneToOneHundredIsRefusedWithTheRange(int percent)
    {
        var user = AddUser();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateAsync(user, Row(AddProject(), percent)));

        Assert.Contains("1 to 100", error.Message);
        Assert.Equal(0, await TotalAsync(user));
    }

    [Fact]
    public async Task OverOneHundredIsRefusedNothingIsSavedAndTheMessageStatesTheTotal()
    {
        var user = AddUser();
        var keep = AddProject();
        await UpdateAsync(user, Row(keep, 30));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => UpdateAsync(user, Row(keep, 60), Row(AddProject(), 50)));

        Assert.Contains("110%", error.Message);
        Assert.Equal(30, await TotalAsync(user));
    }

    [Fact]
    public async Task TheSameProjectTwiceIsRefused()
    {
        var user = AddUser();
        var project = AddProject();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateAsync(user, Row(project, 20), Row(project, 30)));

        Assert.Contains("only appear once", error.Message);
        Assert.Equal(0, await TotalAsync(user));
    }

    [Fact]
    public async Task AMissingProjectOrPositionIsRefused()
    {
        var user = AddUser();
        var project = AddProject();

        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateAsync(user, new ProjectAllocationInput(9999, Member, 50)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateAsync(user, new ProjectAllocationInput(project.Id, 9999, 50)));
        Assert.Equal(0, await TotalAsync(user));
    }

    [Fact]
    public async Task CreatingAUserWithTooMuchAllocationCreatesNoUser()
    {
        var request = new CreateUserRequest(
            null, "New Person", "newperson", "newperson@taskflow.com", "P@ssw0rd!", SystemRoles.EmployeeId, true,
            [Row(AddProject(), 70), Row(AddProject(), 40)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _users.CreateAsync(request, default));

        Assert.False(await _db.Users.AnyAsync(u => u.Username == "newperson"));
    }

    [Fact]
    public async Task CreatingAUserWithAllocationsAndDepartmentsSavesThemTogether()
    {
        var project = AddProject("Created with");
        var department = new Department { Name = "Created dept", CreatedAt = DateTime.UtcNow };
        _db.Departments.Add(department);
        _db.SaveChanges();

        var dto = await _users.CreateAsync(
            new CreateUserRequest(null, "New Person", "newperson2", "newperson2@taskflow.com", "P@ssw0rd!", SystemRoles.EmployeeId, true,
                [Row(project, 40, Qa)], [department.Id]),
            default);

        Assert.Equal(40, dto.TotalAllocatedPercent);
        Assert.Equal(["Created dept"], dto.Departments.Select(d => d.Name));
    }

    // ------------------------------------------------------------------ project side

    private Task<IReadOnlyCollection<ProjectMemberDto>?> ReplaceMembersAsync(Project project, params ProjectMemberInput[] members) =>
        _projects.ReplaceMembersAsync(project.Id, new ReplaceProjectMembersRequest(members), default);

    /// <summary>A team row for a person who manages the project.</summary>
    private static ProjectMemberInput Lead(User user, int position, int percent) => new(user.Id, position, percent, true);

    private static ProjectMemberInput Plain(User user, int position, int percent) => new(user.Id, position, percent);

    private Task<List<int>> LinkedManagersAsync(Project project) =>
        _db.ManagerProjects.Where(link => link.ProjectId == project.Id).Select(link => link.ManagerUserId).OrderBy(id => id).ToListAsync();

    [Fact]
    public async Task AProjectTeamCanBeAssignedWithPositionsAndPercentages()
    {
        var project = AddProject();
        var one = AddManager("teamone");
        var two = AddUser("teamtwo");

        var members = await ReplaceMembersAsync(project, Lead(one, Qa, 60), Plain(two, Developer, 100));

        Assert.Equal(
            [("teamone full name", "QA", 60, true), ("teamtwo full name", "Developer", 100, false)],
            members!.Select(m => (m.FullName, m.PositionName, m.Percent, m.IsManager)));
        Assert.Equal(60, await TotalAsync(one));
    }

    [Fact]
    public async Task ATeamWithMembersNeedsAtLeastOneManagerAndNothingIsSavedWithout()
    {
        var project = AddProject();
        var manager = AddManager();
        var employee = AddUser();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReplaceMembersAsync(project, Plain(manager, Member, 50), Plain(employee, Member, 50)));

        Assert.Contains("manager", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await _db.UserProjectAllocations.Where(a => a.ProjectId == project.Id).ToListAsync());
        Assert.Empty(await LinkedManagersAsync(project));
    }

    [Fact]
    public async Task AnEmptyTeamNeedsNoManagerAndClearsTheFlagsAndLinks()
    {
        var project = AddProject();
        var manager = AddManager();
        await ReplaceMembersAsync(project, Lead(manager, Member, 40));
        Assert.Equal([manager.Id], await LinkedManagersAsync(project));

        var cleared = await ReplaceMembersAsync(project);

        Assert.Empty(cleared!);
        Assert.Empty(await LinkedManagersAsync(project));
    }

    [Fact]
    public async Task OnlyAPersonWithTheManagerRoleCanBeFlagged()
    {
        var project = AddProject();
        var employee = AddUser("justanemployee");
        var manager = AddManager();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReplaceMembersAsync(project, Lead(employee, Member, 50), Lead(manager, Member, 50)));

        Assert.Contains("justanemployee full name", error.Message);
        Assert.Empty(await _db.UserProjectAllocations.Where(a => a.ProjectId == project.Id).ToListAsync());
        Assert.Empty(await LinkedManagersAsync(project));
    }

    [Fact]
    public async Task SeveralManagersCanBeFlaggedAndEachGetsTheProjectLink()
    {
        var project = AddProject();
        var first = AddManager();
        var second = AddManager();
        var employee = AddUser();

        var members = await ReplaceMembersAsync(project, Lead(first, Member, 20), Lead(second, Member, 20), Plain(employee, Member, 60));

        Assert.Equal(2, members!.Count(m => m.IsManager));
        Assert.Equal(new[] { first.Id, second.Id }.Order().ToList(), await LinkedManagersAsync(project));
    }

    [Fact]
    public async Task TheManagerLinkFollowsTheFlagWhenTheManagerChangesOrLeaves()
    {
        var project = AddProject();
        var maya = AddManager();
        var ravi = AddManager();
        var employee = AddUser();
        await ReplaceMembersAsync(project, Lead(maya, Member, 30), Plain(ravi, Member, 30), Plain(employee, Member, 40));
        Assert.Equal([maya.Id], await LinkedManagersAsync(project));

        await ReplaceMembersAsync(project, Plain(maya, Member, 30), Lead(ravi, Member, 30), Plain(employee, Member, 40));
        Assert.Equal([ravi.Id], await LinkedManagersAsync(project));

        await ReplaceMembersAsync(project, Lead(maya, Member, 30), Plain(employee, Member, 40));
        Assert.Equal([maya.Id], await LinkedManagersAsync(project));
    }

    [Fact]
    public async Task TheProjectListShowsItsManagersByName()
    {
        var project = AddProject("Visible managers");
        var maya = AddManager("maya");
        var ravi = AddManager("ravi");
        await ReplaceMembersAsync(project, Lead(ravi, Member, 20), Lead(maya, Member, 20));

        var listed = (await _projects.GetAllAsync(default)).Single(p => p.Id == project.Id);

        Assert.Equal(["maya full name", "ravi full name"], listed.Managers);
    }

    [Fact]
    public async Task ProjectSideAssignmentRespectsWhatThePersonHasElsewhere()
    {
        var busy = AddManager("busy");
        await UpdateAsync(busy, Row(AddProject(), 70));
        var target = AddProject();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReplaceMembersAsync(target, Lead(busy, Member, 50)));

        Assert.Contains("busy full name", error.Message);
        Assert.Contains("120%", error.Message);
        Assert.Contains("30% remains", error.Message);
        Assert.Equal(70, await TotalAsync(busy));
        Assert.Empty(await _db.UserProjectAllocations.Where(a => a.ProjectId == target.Id).ToListAsync());

        var fits = await ReplaceMembersAsync(target, Lead(busy, Member, 30));
        Assert.Equal(30, fits!.Single().Percent);
        Assert.Equal(100, await TotalAsync(busy));
    }

    [Fact]
    public async Task OneRowOverTheCapRejectsTheWholeProjectRequest()
    {
        var project = AddProject();
        var fine = AddManager();
        var full = AddUser();
        await UpdateAsync(full, Row(AddProject(), 100));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReplaceMembersAsync(project, Lead(fine, Member, 20), Plain(full, Member, 10)));

        Assert.Contains("110%", error.Message);
        Assert.Empty(await _db.UserProjectAllocations.Where(a => a.ProjectId == project.Id).ToListAsync());
    }

    [Fact]
    public async Task ReplacingTheTeamEditsKeepsAndRemovesMembersAndFreesTheirPercentage()
    {
        var project = AddProject();
        var stays = AddManager();
        var leaves = AddUser();
        await ReplaceMembersAsync(project, Lead(stays, Member, 40), Plain(leaves, Member, 60));

        var members = await ReplaceMembersAsync(project, Lead(stays, Developer, 50));

        var only = Assert.Single(members!);
        Assert.Equal((stays.Id, "Developer", 50), (only.UserId, only.PositionName, only.Percent));
        Assert.Equal(0, await TotalAsync(leaves));

        await ReplaceMembersAsync(project);
        Assert.Empty((await _projects.GetMembersAsync(project.Id, default))!);
    }

    [Fact]
    public async Task ProjectSideRowsAreValidated()
    {
        var project = AddProject();
        var user = AddManager();
        var inactive = AddManager(active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ReplaceMembersAsync(project, Lead(user, Member, 0)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReplaceMembersAsync(project, Lead(user, Member, 101)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReplaceMembersAsync(project, Lead(user, Member, 10), Lead(user, Qa, 10)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReplaceMembersAsync(project, Lead(user, 9999, 10)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReplaceMembersAsync(project, Lead(inactive, Member, 10)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReplaceMembersAsync(project, new ProjectMemberInput(9999, Member, 10, true)));
        Assert.Null(await ReplaceMembersAsync(new Project { Id = 9999 }));
    }

    [Fact]
    public async Task ABrokenRequestLeavesTheExistingTeamUntouched()
    {
        var project = AddProject();
        var user = AddManager();
        await ReplaceMembersAsync(project, Lead(user, Member, 25));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReplaceMembersAsync(project, Lead(user, Member, 25), new ProjectMemberInput(9999, Member, 5)));

        Assert.Equal(25, Assert.Single((await _projects.GetMembersAsync(project.Id, default))!).Percent);
        Assert.Equal([user.Id], await LinkedManagersAsync(project));
    }

    [Fact]
    public async Task TwoAssignersCannotBothPushTheSamePersonOverTheCap()
    {
        var person = AddManager();
        var first = AddProject();
        var second = AddProject();

        // Two requests for the same person, one after the other, as the transaction would serialise them.
        await ReplaceMembersAsync(first, Lead(person, Member, 70));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReplaceMembersAsync(second, Lead(person, Member, 60)));

        Assert.Equal(70, await TotalAsync(person));
    }

    [Fact]
    public async Task AnEmployeesManagersIncludeTheProjectManagerWithoutDuplicates()
    {
        var project = AddProject();
        var maya = AddManager("maya");
        var ravi = AddManager("ravi");
        var employee = AddUser("priya");
        var department = new Department { Name = "Shared dept", CreatedAt = DateTime.UtcNow };
        _db.Departments.Add(department);
        _db.SaveChanges();
        _db.DepartmentManagers.Add(new DepartmentManager { DepartmentId = department.Id, ManagerUserId = maya.Id });
        _db.UserDepartments.Add(new UserDepartment { DepartmentId = department.Id, UserId = employee.Id });
        _db.SaveChanges();

        await ReplaceMembersAsync(project, Lead(maya, Member, 20), Lead(ravi, Member, 20), Plain(employee, Developer, 60));

        var dto = await _users.GetByIdAsync(employee.Id, default);
        Assert.Equal(["maya full name", "ravi full name"], dto!.Managers.Select(m => m.FullName));
    }

    [Fact]
    public async Task AManagersReportsIncludeEveryoneOnTheirProjectsButNotThemselves()
    {
        var project = AddProject();
        var maya = AddManager("maya");
        var priya = AddUser("priya");
        var omar = AddUser("omar");
        await ReplaceMembersAsync(project, Lead(maya, Member, 20), Plain(priya, Developer, 40), Plain(omar, Qa, 40));

        var dto = await _users.GetByIdAsync(maya.Id, default);

        Assert.Equal(["omar full name", "priya full name"], dto!.Reports.Select(r => r.FullName));
        Assert.Empty((await _users.GetByIdAsync(priya.Id, default))!.Reports);
    }

    [Fact]
    public async Task AnUnflaggedManagerOnTheTeamIsNotAManagerOfTheOthers()
    {
        var project = AddProject();
        var lead = AddManager("lead");
        var peer = AddManager("peer");
        var employee = AddUser("priya");
        await ReplaceMembersAsync(project, Lead(lead, Member, 20), Plain(peer, Member, 20), Plain(employee, Developer, 60));

        var dto = await _users.GetByIdAsync(employee.Id, default);

        Assert.Equal(["lead full name"], dto!.Managers.Select(m => m.FullName));
    }

    [Fact]
    public async Task LeavingAProjectThroughTheEmployeeFormEndsManagingIt()
    {
        var project = AddProject();
        var manager = AddManager();
        await ReplaceMembersAsync(project, Lead(manager, Member, 40));

        await UpdateAsync(manager);

        Assert.Empty(await LinkedManagersAsync(project));
    }

    // ------------------------------------------------------------------ deleting projects

    [Fact]
    public async Task DeletingAProjectRemovesItsAllocationsAndFreesThePercentages()
    {
        var user = AddUser();
        var gone = AddProject();
        var kept = AddProject();
        await UpdateAsync(user, Row(gone, 60), Row(kept, 40));

        Assert.True(await _projects.DeleteAsync(gone.Id, default));

        var dto = await _users.GetByIdAsync(user.Id, default);
        Assert.Equal([kept.Id], dto!.ProjectAllocations.Select(a => a.ProjectId));
        Assert.Equal(60, dto.RemainingPercent);
        Assert.Empty(await _db.UserProjectAllocations.IgnoreQueryFilters().Where(a => a.ProjectId == gone.Id).ToListAsync());
    }

    // ------------------------------------------------------------------ filters that match any membership

    [Fact]
    public async Task TheProjectAndDepartmentFiltersMatchAnyMembershipAndListEachPersonOnce()
    {
        var multi = AddUser("multi");
        var other = AddUser("other");
        var a = AddProject();
        var b = AddProject();
        await UpdateAsync(multi, Row(a, 50), Row(b, 50));
        await UpdateAsync(other, Row(a, 10));
        var dept1 = new Department { Name = "Filter 1", CreatedAt = DateTime.UtcNow };
        var dept2 = new Department { Name = "Filter 2", CreatedAt = DateTime.UtcNow };
        _db.Departments.AddRange(dept1, dept2);
        _db.SaveChanges();
        TestMemberships.JoinDepartment(_db, multi, dept1.Id);
        TestMemberships.JoinDepartment(_db, multi, dept2.Id);

        var onB = await _users.GetAllAsync(new UserListFilter(null, b.Id, null, null), default);
        var onA = await _users.GetAllAsync(new UserListFilter(null, a.Id, null, null), default);
        var inDept2 = await _users.GetAllAsync(new UserListFilter(null, null, null, null, dept2.Id), default);

        Assert.Equal(["multi"], onB.Select(u => u.Username));
        Assert.Equal(["multi", "other"], onA.Select(u => u.Username).Order());
        Assert.Equal(["multi"], inDept2.Select(u => u.Username));
    }

    // ------------------------------------------------------------------ positions

    [Fact]
    public async Task APositionIsCreatedOnceWhateverTheCaseAndTheSeededOnesExist()
    {
        var positions = new PositionService(_db);

        var existing = await positions.GetOrCreateAsync("qa", default);
        var created = await positions.GetOrCreateAsync("  Scrum   Master ", default);
        var again = await positions.GetOrCreateAsync("scrum master", default);

        Assert.Equal(Qa, existing.Id);
        Assert.Equal("QA", existing.Name);
        Assert.Equal("Scrum Master", created.Name);
        Assert.Equal(created.Id, again.Id);
        Assert.Equal(1, await _db.Positions.CountAsync(p => p.Name == "Scrum Master"));
        Assert.Contains("DevOps", (await positions.GetAllAsync(default)).Select(p => p.Name));
    }

    [Fact]
    public async Task ABlankOrTooLongPositionNameIsRefused()
    {
        var positions = new PositionService(_db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => positions.GetOrCreateAsync("   ", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => positions.GetOrCreateAsync(new string('x', 101), default));
    }

    [Fact]
    public async Task APositionLabelGrantsNothing()
    {
        var user = AddUser();
        var project = AddProject();
        await UpdateAsync(user, new ProjectAllocationInput(project.Id, 2, 100)); // 2 = Manager

        var reloaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(SystemRoles.EmployeeId, reloaded.RoleId);
        Assert.False(await _db.ManagerProjects.AnyAsync(link => link.ManagerUserId == user.Id));
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
