using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;

namespace TaskFlow.UnitTests;

public class ManagerAssignmentTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly UserService _service;
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    public ManagerAssignmentTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        TestAccessControl.Seed(_db);
        _service = new UserService(_db, _hasher);
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
            ManagerId = managerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SecurityStamp = "stamp-1"
        };
        user.PasswordHash = _hasher.HashPassword(user, "P@ssw0rd!");
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private Department AddDepartment(string name)
    {
        var department = new Department { Name = name, CreatedAt = DateTime.UtcNow };
        _db.Departments.Add(department);
        _db.SaveChanges();
        return department;
    }

    private Task<UserDto?> AssignAsync(int userId, int? managerId, params int[] departmentIds) =>
        _service.UpdateManagerAssignmentsAsync(userId, new ManagerAssignmentRequest(managerId, departmentIds), default);

    private Task<List<int>> DepartmentIdsAsync(int managerId) =>
        _db.ManagerDepartments.Where(link => link.ManagerUserId == managerId).Select(link => link.DepartmentId).OrderBy(id => id).ToListAsync();

    // ------------------------------------------------------------------ departments

    [Fact]
    public async Task AManagerCanHaveSeveralDepartmentsAndTheyAreListedWithTheirNames()
    {
        var manager = AddUser("mgr1", SystemRoles.ManagerId);
        var finance = AddDepartment("Finance");
        var ops = AddDepartment("Operations");
        var hr = AddDepartment("HR");

        var dto = await AssignAsync(manager.Id, null, finance.Id, ops.Id, hr.Id);

        Assert.Equal(["Finance", "HR", "Operations"], dto!.ManagerDepartments!.Select(d => d.Name));
        Assert.Equal(3, (await DepartmentIdsAsync(manager.Id)).Count);
    }

    [Fact]
    public async Task SavingADifferentSetReplacesTheOldOneAndAnEmptySetRemovesAll()
    {
        var manager = AddUser("mgr2", SystemRoles.ManagerId);
        var a = AddDepartment("A");
        var b = AddDepartment("B");
        var c = AddDepartment("C");
        await AssignAsync(manager.Id, null, a.Id, b.Id);

        await AssignAsync(manager.Id, null, b.Id, c.Id);
        Assert.Equal([b.Id, c.Id], await DepartmentIdsAsync(manager.Id));

        await AssignAsync(manager.Id, null);
        Assert.Empty(await DepartmentIdsAsync(manager.Id));
    }

    [Fact]
    public async Task ADepartmentCanHaveSeveralManagers()
    {
        var one = AddUser("mgr3a", SystemRoles.ManagerId);
        var two = AddUser("mgr3b", SystemRoles.ManagerId);
        var shared = AddDepartment("Shared");

        await AssignAsync(one.Id, null, shared.Id);
        await AssignAsync(two.Id, null, shared.Id);

        Assert.Equal(2, await _db.ManagerDepartments.CountAsync(link => link.DepartmentId == shared.Id));
    }

    [Fact]
    public async Task OnlyAManagerCanBeGivenDepartments()
    {
        var employee = AddUser("emp4");
        var admin = AddUser("adm4", SystemRoles.AdministratorId);
        var department = AddDepartment("Dept 4");

        await Assert.ThrowsAsync<InvalidOperationException>(() => AssignAsync(employee.Id, null, department.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AssignAsync(admin.Id, null, department.Id));
        Assert.False(await _db.ManagerDepartments.AnyAsync());
    }

    [Fact]
    public async Task AnEmptyDepartmentSetIsFineForAnyone()
    {
        var employee = AddUser("emp5");

        var dto = await AssignAsync(employee.Id, null);

        Assert.NotNull(dto);
        Assert.Empty(dto!.ManagerDepartments!);
    }

    [Fact]
    public async Task AMissingDepartmentIsRejectedAndNothingIsSaved()
    {
        var manager = AddUser("mgr6", SystemRoles.ManagerId);
        var real = AddDepartment("Real");

        await Assert.ThrowsAsync<InvalidOperationException>(() => AssignAsync(manager.Id, null, real.Id, 9999));

        Assert.Empty(await DepartmentIdsAsync(manager.Id));
    }

    [Fact]
    public async Task ADeletedDepartmentDisappearsFromTheManagersDepartments()
    {
        var manager = AddUser("mgr7", SystemRoles.ManagerId);
        var keep = AddDepartment("Keep");
        var drop = AddDepartment("Drop");
        await AssignAsync(manager.Id, null, keep.Id, drop.Id);

        Assert.True(await new DepartmentService(_db).DeleteAsync(drop.Id, default));

        var dto = await _service.GetByIdAsync(manager.Id, default);
        Assert.Equal(["Keep"], dto!.ManagerDepartments!.Select(d => d.Name));
        Assert.Equal([keep.Id], await DepartmentIdsAsync(manager.Id));
    }

    [Fact]
    public async Task ChangingAManagersRoleRemovesTheirDepartmentsAndTheirReportsLoseTheManager()
    {
        var manager = AddUser("mgr8", SystemRoles.ManagerId);
        var report = AddUser("rep8", managerId: manager.Id);
        var department = AddDepartment("Dept 8");
        await AssignAsync(manager.Id, null, department.Id);
        var request = new UpdateUserRequest(null, manager.FullName, manager.Username, manager.Email, SystemRoles.EmployeeId, null);

        await _service.UpdateAsync(manager.Id, request, default);

        Assert.Empty(await DepartmentIdsAsync(manager.Id));
        Assert.Null((await _db.Users.AsNoTracking().SingleAsync(u => u.Id == report.Id)).ManagerId);
    }

    [Fact]
    public async Task EditingAManagerWithoutChangingTheRoleKeepsTheirAssignments()
    {
        var manager = AddUser("mgr9", SystemRoles.ManagerId);
        var report = AddUser("rep9", managerId: manager.Id);
        var department = AddDepartment("Dept 9");
        await AssignAsync(manager.Id, null, department.Id);
        var request = new UpdateUserRequest(null, "Renamed", manager.Username, manager.Email, SystemRoles.ManagerId, null);

        await _service.UpdateAsync(manager.Id, request, default);

        Assert.Equal([department.Id], await DepartmentIdsAsync(manager.Id));
        Assert.Equal(manager.Id, (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == report.Id)).ManagerId);
    }

    // ------------------------------------------------------------------ employees

    [Fact]
    public async Task AnEmployeeCanBeAssignedReassignedAndClearedWithTheManagersNameShown()
    {
        var first = AddUser("mgr10a", SystemRoles.ManagerId);
        var second = AddUser("mgr10b", SystemRoles.ManagerId);
        var employee = AddUser("emp10");

        var assigned = await AssignAsync(employee.Id, first.Id);
        Assert.Equal(first.Id, assigned!.ManagerId);
        Assert.Equal("mgr10a full name", assigned.ManagerName);

        var moved = await AssignAsync(employee.Id, second.Id);
        Assert.Equal(second.Id, moved!.ManagerId);

        var cleared = await AssignAsync(employee.Id, null);
        Assert.Null(cleared!.ManagerId);
        Assert.Null(cleared.ManagerName);
    }

    [Fact]
    public async Task AManagerHasAnyNumberOfEmployeesAndTheyAreListedOnTheManager()
    {
        var manager = AddUser("mgr11", SystemRoles.ManagerId);
        var one = AddUser("emp11a");
        var two = AddUser("emp11b");
        var three = AddUser("emp11c");

        foreach (var employee in new[] { one, two, three })
        {
            await AssignAsync(employee.Id, manager.Id);
        }

        var dto = await _service.GetByIdAsync(manager.Id, default);
        Assert.Equal(["emp11a full name", "emp11b full name", "emp11c full name"], dto!.Reports!.Select(r => r.FullName));
    }

    [Fact]
    public async Task TheManagerMustBeAnActiveManager()
    {
        var employee = AddUser("emp12");
        var otherEmployee = AddUser("emp12b");
        var admin = AddUser("adm12", SystemRoles.AdministratorId);
        var inactive = AddUser("mgr12-off", SystemRoles.ManagerId, active: false);
        var gone = AddUser("mgr12-gone", SystemRoles.ManagerId);
        _db.Users.Single(u => u.Id == gone.Id).IsDeleted = true;
        _db.SaveChanges();

        foreach (var target in new[] { otherEmployee.Id, admin.Id, inactive.Id, gone.Id, 9999 })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => AssignAsync(employee.Id, target));
        }

        Assert.Null((await _db.Users.AsNoTracking().SingleAsync(u => u.Id == employee.Id)).ManagerId);
    }

    [Fact]
    public async Task NobodyIsTheirOwnManagerAndManagersCannotManageEachOtherInACircle()
    {
        var one = AddUser("mgr13a", SystemRoles.ManagerId);
        var two = AddUser("mgr13b", SystemRoles.ManagerId);
        var three = AddUser("mgr13c", SystemRoles.ManagerId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => AssignAsync(one.Id, one.Id));

        await AssignAsync(two.Id, one.Id);
        await AssignAsync(three.Id, two.Id);
        var loop = await Assert.ThrowsAsync<InvalidOperationException>(() => AssignAsync(one.Id, three.Id));
        Assert.Contains("each other", loop.Message);
    }

    [Fact]
    public async Task AnUnknownOrDeletedUserReturnsNull()
    {
        var gone = AddUser("emp14");
        _db.Users.Single(u => u.Id == gone.Id).IsDeleted = true;
        _db.SaveChanges();

        Assert.Null(await AssignAsync(9999, null));
        Assert.Null(await AssignAsync(gone.Id, null));
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
