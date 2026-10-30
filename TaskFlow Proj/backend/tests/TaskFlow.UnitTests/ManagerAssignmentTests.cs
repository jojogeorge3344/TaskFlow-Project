using TaskFlow.Api.Services.Management.EmployeeManagement.Allocations;
using TaskFlow.Api.Services.Management.EmployeeManagement;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Repositories.Management.ProjectManagement.Positions;
using TaskFlow.Api.Repositories.Management.ProjectManagement;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Services.Management.ProjectManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

public class ManagerAssignmentTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly EmployeeService _service;
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    public ManagerAssignmentTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        TestAccessControl.Seed(_db);
        _service = TestServices.Employees(_db, _hasher);
    }

    private User AddUser(string username, int roleId = SystemRoles.EmployeeId, bool active = true, int? managerId = null)
    {
        var user = new User
        {
            FullName = $"{username} full name",
            Username = username,
            Email = $"{username}@taskflow.com",
            RoleId = roleId,
            IsActive = active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SecurityStamp = "stamp-1"
        };
        user.PasswordHash = _hasher.HashPassword(user, "P@ssw0rd!");
        _db.Users.Add(user);
        _db.SaveChanges();
        if (managerId is int manager)
        {
            TestMemberships.ReportTo(_db, user, manager);
        }

        return user;
    }

    private Department AddDepartment(string name, params User[] managers)
    {
        var department = new Department { Name = name, CreatedAt = DateTime.UtcNow };
        _db.Departments.Add(department);
        _db.SaveChanges();
        foreach (var manager in managers)
        {
            _db.DepartmentManagers.Add(new DepartmentManager { ManagerUserId = manager.Id, DepartmentId = department.Id });
        }

        _db.SaveChanges();
        return department;
    }

    private Project AddProject(string name)
    {
        var project = new Project { Name = name, CreatedAt = DateTime.UtcNow };
        _db.Projects.Add(project);
        _db.SaveChanges();
        return project;
    }

    /// <summary>Sets the projects a manager oversees directly. The projects normally come from the flags on a project's team.</summary>
    private async Task<UserDto?> AssignAsync(int userId, params int[] projectIds)
    {
        _db.ManagerProjects.RemoveRange(_db.ManagerProjects.Where(link => link.ManagerUserId == userId));
        foreach (var projectId in projectIds)
        {
            _db.ManagerProjects.Add(new ManagerProject { ManagerUserId = userId, ProjectId = projectId });
        }

        await _db.SaveChangesAsync();
        return await _service.GetByIdAsync(userId, default);
    }

    private Task<List<int>> ProjectIdsAsync(int managerId) =>
        _db.ManagerProjects.Where(link => link.ManagerUserId == managerId).Select(link => link.ProjectId).OrderBy(id => id).ToListAsync();

    // ------------------------------------------------------------------ projects

    [Fact]
    public async Task AManagerCanHaveSeveralProjectsAndTheyAreListedWithTheirNames()
    {
        var manager = AddUser("mgr1", SystemRoles.ManagerId);
        var finance = AddProject("Finance");
        var ops = AddProject("Operations");
        var hr = AddProject("HR");

        var dto = await AssignAsync(manager.Id, finance.Id, ops.Id, hr.Id);

        Assert.Equal(["Finance", "HR", "Operations"], dto!.ManagerProjects!.Select(d => d.Name));
        Assert.Equal(3, (await ProjectIdsAsync(manager.Id)).Count);
    }

    [Fact]
    public async Task ADeletedProjectDisappearsFromTheManagersProjects()
    {
        var manager = AddUser("mgr7", SystemRoles.ManagerId);
        var keep = AddProject("Keep");
        var drop = AddProject("Drop");
        await AssignAsync(manager.Id, keep.Id, drop.Id);

        Assert.True(await TestServices.Projects(_db).DeleteAsync(drop.Id, default));

        var dto = await _service.GetByIdAsync(manager.Id, default);
        Assert.Equal(["Keep"], dto!.ManagerProjects!.Select(d => d.Name));
        Assert.Equal([keep.Id], await ProjectIdsAsync(manager.Id));
    }

    [Fact]
    public async Task ChangingAManagersRoleRemovesTheirProjectsAndTheirReportsLoseTheManager()
    {
        var manager = AddUser("mgr8", SystemRoles.ManagerId);
        var report = AddUser("rep8", managerId: manager.Id);
        var project = AddProject("Dept 8");
        await AssignAsync(manager.Id, project.Id);
        Assert.Equal([manager.Id], (await _service.GetByIdAsync(report.Id, default))!.Managers.Select(m => m.Id));
        var request = new UpdateUserRequest(null, manager.FullName, manager.Username, manager.Email, SystemRoles.EmployeeId);

        await _service.UpdateAsync(manager.Id, request, default);

        Assert.Empty(await ProjectIdsAsync(manager.Id));
        Assert.Empty((await _service.GetByIdAsync(report.Id, default))!.Managers);
    }

    [Fact]
    public async Task EditingAManagerWithoutChangingTheRoleKeepsTheirAssignments()
    {
        var manager = AddUser("mgr9", SystemRoles.ManagerId);
        var report = AddUser("rep9", managerId: manager.Id);
        var project = AddProject("Dept 9");
        await AssignAsync(manager.Id, project.Id);
        var request = new UpdateUserRequest(null, "Renamed", manager.Username, manager.Email, SystemRoles.ManagerId);

        await _service.UpdateAsync(manager.Id, request, default);

        Assert.Equal([project.Id], await ProjectIdsAsync(manager.Id));
        Assert.Equal([manager.Id], (await _service.GetByIdAsync(report.Id, default))!.Managers.Select(m => m.Id));
    }

    // ------------------------------------------------------------------ derived managers

    [Fact]
    public async Task AUsersManagersAreTheManagersOfTheirDepartments()
    {
        var first = AddUser("mgr10a", SystemRoles.ManagerId);
        var second = AddUser("mgr10b", SystemRoles.ManagerId);
        var outsider = AddUser("mgr10c", SystemRoles.ManagerId);
        var employee = AddUser("emp10");
        var finance = AddDepartment("Finance 10", first);
        var ops = AddDepartment("Ops 10", second);
        AddDepartment("Elsewhere 10", outsider);
        TestMemberships.JoinDepartment(_db, employee, finance.Id);
        TestMemberships.JoinDepartment(_db, employee, ops.Id);

        var dto = await _service.GetByIdAsync(employee.Id, default);

        Assert.Equal(["mgr10a full name", "mgr10b full name"], dto!.Managers.Select(m => m.FullName));
    }

    [Fact]
    public async Task AManagerOfTwoOfTheUsersDepartmentsIsListedOnce()
    {
        var manager = AddUser("mgr11", SystemRoles.ManagerId);
        var employee = AddUser("emp11");
        TestMemberships.JoinDepartment(_db, employee, AddDepartment("A 11", manager).Id);
        TestMemberships.JoinDepartment(_db, employee, AddDepartment("B 11", manager).Id);

        var dto = await _service.GetByIdAsync(employee.Id, default);

        Assert.Equal([manager.Id], dto!.Managers.Select(m => m.Id));
    }

    [Fact]
    public async Task AUserWithNoDepartmentOrAnUnmanagedDepartmentHasNoManagersAndNobodyManagesThemself()
    {
        var manager = AddUser("mgr12", SystemRoles.ManagerId);
        var loner = AddUser("emp12");
        var unmanaged = AddUser("emp12b");
        TestMemberships.JoinDepartment(_db, unmanaged, AddDepartment("Nobody 12").Id);
        TestMemberships.JoinDepartment(_db, manager, AddDepartment("Self 12", manager).Id);

        Assert.Empty((await _service.GetByIdAsync(loner.Id, default))!.Managers);
        Assert.Empty((await _service.GetByIdAsync(unmanaged.Id, default))!.Managers);
        Assert.Empty((await _service.GetByIdAsync(manager.Id, default))!.Managers);
    }

    [Fact]
    public async Task AManagersReportsAreThePeopleInTheDepartmentsTheyManage()
    {
        var manager = AddUser("mgr13", SystemRoles.ManagerId);
        var one = AddUser("emp13a");
        var two = AddUser("emp13b");
        var other = AddUser("emp13c");
        var team = AddDepartment("Team 13", manager);
        TestMemberships.JoinDepartment(_db, one, team.Id);
        TestMemberships.JoinDepartment(_db, two, team.Id);
        TestMemberships.JoinDepartment(_db, other, AddDepartment("Other 13").Id);

        var dto = await _service.GetByIdAsync(manager.Id, default);

        Assert.Equal(["emp13a full name", "emp13b full name"], dto!.Reports!.Select(r => r.FullName));
    }

    [Fact]
    public async Task AnUnknownOrDeletedUserReturnsNull()
    {
        var gone = AddUser("emp14");
        _db.Users.Single(u => u.Id == gone.Id).IsDeleted = true;
        _db.SaveChanges();

        Assert.Null(await AssignAsync(9999));
        Assert.Null(await AssignAsync(gone.Id));
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
