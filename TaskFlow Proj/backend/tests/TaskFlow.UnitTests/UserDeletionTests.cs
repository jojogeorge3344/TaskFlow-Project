using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Users;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class UserDeletionTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly UserService _service;
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    public UserDeletionTests()
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

    private TaskEntity AddTask(User assignee, User creator, TaskStatus status, bool deleted = false)
    {
        var task = new TaskEntity
        {
            TaskCode = $"T-{Guid.NewGuid():N}"[..20],
            Title = "A task",
            AssignedToUserId = assignee.Id,
            CreatedByUserId = creator.Id,
            Priority = TaskPriority.Medium,
            StartDate = DateTime.UtcNow.AddDays(-1),
            Deadline = DateTime.UtcNow.AddDays(3),
            EstimatedHours = 1,
            Status = status,
            IsDeleted = deleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Tasks.Add(task);
        _db.SaveChanges();
        return task;
    }

    private Task<User> ReloadAsync(int id) => _db.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == id);

    [Fact]
    public async Task Delete_SoftDeletesAndKeepsTheRowEmailAndUsername()
    {
        var admin = AddUser("admin1", SystemRoles.AdministratorId);
        var employee = AddUser("emp1");

        var deleted = await _service.DeleteAsync(employee.Id, admin.Id, default);

        Assert.True(deleted);
        var row = await ReloadAsync(employee.Id);
        Assert.True(row.IsDeleted);
        Assert.False(row.IsActive);
        Assert.Equal("emp1@taskflow.com", row.Email);
        Assert.Equal("emp1", row.Username);
        Assert.NotEqual("stamp-1", row.SecurityStamp);
        Assert.Null(await _service.GetByIdAsync(employee.Id, default));
    }

    [Fact]
    public async Task Delete_ReturnsFalseForUnknownAndAlreadyDeletedUsers()
    {
        var admin = AddUser("admin2", SystemRoles.AdministratorId);
        var employee = AddUser("emp2");

        Assert.False(await _service.DeleteAsync(9999, admin.Id, default));
        Assert.True(await _service.DeleteAsync(employee.Id, admin.Id, default));
        Assert.False(await _service.DeleteAsync(employee.Id, admin.Id, default));
    }

    [Fact]
    public async Task Delete_RevokesActiveRefreshTokens()
    {
        var admin = AddUser("admin3", SystemRoles.AdministratorId);
        var employee = AddUser("emp3");
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = employee.Id,
            TokenHash = "hash-1",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        _db.SaveChanges();

        await _service.DeleteAsync(employee.Id, admin.Id, default);

        var token = await _db.RefreshTokens.IgnoreQueryFilters().AsNoTracking().SingleAsync(t => t.UserId == employee.Id);
        Assert.NotNull(token.RevokedAt);
    }

    [Fact]
    public async Task Delete_RejectsDeletingYourself()
    {
        var admin = AddUser("admin4", SystemRoles.AdministratorId);
        AddUser("admin4b", SystemRoles.AdministratorId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(admin.Id, admin.Id, default));

        Assert.Contains("own account", ex.Message);
        Assert.False((await ReloadAsync(admin.Id)).IsDeleted);
    }

    [Fact]
    public async Task Delete_RejectsTheLastActiveAdministrator()
    {
        var caller = AddUser("caller", SystemRoles.AdministratorId, active: false);
        var onlyAdmin = AddUser("only-admin", SystemRoles.AdministratorId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(onlyAdmin.Id, caller.Id, default));

        Assert.Contains("only active Administrator", ex.Message);
        Assert.False((await ReloadAsync(onlyAdmin.Id)).IsDeleted);
    }

    [Fact]
    public async Task Delete_AllowsAnAdministratorWhenAnotherActiveOneExists()
    {
        var caller = AddUser("caller2", SystemRoles.AdministratorId);
        var other = AddUser("other-admin", SystemRoles.AdministratorId);

        Assert.True(await _service.DeleteAsync(other.Id, caller.Id, default));
    }

    [Fact]
    public async Task Delete_RejectsAnEmployeeWithUnfinishedTasksAndAllowsOnceTheyAreDone()
    {
        var admin = AddUser("admin5", SystemRoles.AdministratorId);
        var employee = AddUser("emp5");
        var open = AddTask(employee, admin, TaskStatus.InProgress);
        AddTask(employee, admin, TaskStatus.Cancelled);
        AddTask(employee, admin, TaskStatus.ToDo, deleted: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(employee.Id, admin.Id, default));
        Assert.Contains("1 unfinished task", ex.Message);
        Assert.False((await ReloadAsync(employee.Id)).IsDeleted);

        open.Status = TaskStatus.Completed;
        _db.SaveChanges();

        Assert.True(await _service.DeleteAsync(employee.Id, admin.Id, default));
    }

    [Fact]
    public async Task Delete_KeepsTheTasksTheEmployeeCreatedAndCommentedOn()
    {
        var admin = AddUser("admin6", SystemRoles.AdministratorId);
        var employee = AddUser("emp6");
        var done = AddTask(admin, employee, TaskStatus.Completed);
        _db.TaskComments.Add(new TaskComment { TaskId = done.Id, AuthorUserId = employee.Id, Body = "Looks good", CreatedAt = DateTime.UtcNow });
        _db.SaveChanges();

        await _service.DeleteAsync(employee.Id, admin.Id, default);

        Assert.True(await _db.Tasks.AnyAsync(t => t.Id == done.Id && !t.IsDeleted));
        Assert.True(await _db.TaskComments.AnyAsync(c => c.TaskId == done.Id && c.AuthorUserId == employee.Id));
    }

    [Fact]
    public async Task Delete_ClearsTheManagerLinksOfAManagersReportsAndTheirProjects()
    {
        var admin = AddUser("admin7", SystemRoles.AdministratorId);
        var manager = AddUser("mgr7", SystemRoles.ManagerId);
        var report = AddUser("rep7", managerId: manager.Id);
        var project = new Project { Name = "Ops", CreatedAt = DateTime.UtcNow };
        _db.Projects.Add(project);
        _db.SaveChanges();
        _db.ManagerProjects.Add(new ManagerProject { ManagerUserId = manager.Id, ProjectId = project.Id });
        _db.SaveChanges();

        await _service.DeleteAsync(manager.Id, admin.Id, default);

        Assert.Null((await ReloadAsync(report.Id)).ManagerId);
        Assert.False(await _db.ManagerProjects.AnyAsync(link => link.ManagerUserId == manager.Id));
    }

    [Fact]
    public async Task Delete_ClearsTheDeletedEmployeesOwnManager()
    {
        var admin = AddUser("admin8", SystemRoles.AdministratorId);
        var manager = AddUser("mgr8", SystemRoles.ManagerId);
        var employee = AddUser("emp8", managerId: manager.Id);

        await _service.DeleteAsync(employee.Id, admin.Id, default);

        Assert.Null((await ReloadAsync(employee.Id)).ManagerId);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
