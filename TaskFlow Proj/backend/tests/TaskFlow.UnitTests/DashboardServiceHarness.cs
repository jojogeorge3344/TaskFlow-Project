using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Services;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

internal sealed class FakeTimeProvider : TimeProvider
{
    public FakeTimeProvider(DateTimeOffset now)
    {
        UtcNow = now;
    }

    public DateTimeOffset UtcNow { get; set; }

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

internal sealed class DashboardServiceHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private DashboardServiceHarness(
        SqliteConnection connection,
        AppDbContext db,
        DashboardService service,
        FakeTimeProvider clock)
    {
        _connection = connection;
        Db = db;
        Service = service;
        Clock = clock;
    }

    public AppDbContext Db { get; }

    public DashboardService Service { get; }

    public FakeTimeProvider Clock { get; }

    public static DashboardServiceHarness Create(DateTimeOffset? now = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        var clock = new FakeTimeProvider(now ?? new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var service = new DashboardService(db, clock);

        return new DashboardServiceHarness(connection, db, service, clock);
    }

    public Department AddDepartment(string name)
    {
        var department = new Department
        {
            Name = name,
            CreatedAt = Clock.UtcNow.UtcDateTime
        };

        Db.Departments.Add(department);
        Db.SaveChanges();

        return department;
    }

    public User AddUser(
        string username,
        bool isActive = true,
        Role role = Role.Employee,
        int? departmentId = null)
    {
        var user = new User
        {
            FullName = $"{username} full name",
            Username = username,
            Email = $"{username}@taskflow.local",
            PasswordHash = "hash",
            Role = role,
            IsActive = isActive,
            DepartmentId = departmentId,
            CreatedAt = Clock.UtcNow.UtcDateTime,
            UpdatedAt = Clock.UtcNow.UtcDateTime
        };

        Db.Users.Add(user);
        Db.SaveChanges();

        return user;
    }

    public TaskEntity AddTask(
        int assignedToUserId,
        DateTime deadline,
        TaskStatus status,
        int? createdByUserId = null,
        int? departmentId = null,
        bool isDeleted = false)
    {
        var task = new TaskEntity
        {
            TaskCode = $"T{Guid.NewGuid():N}"[..8],
            Title = "Task",
            AssignedToUserId = assignedToUserId,
            CreatedByUserId = createdByUserId ?? assignedToUserId,
            DepartmentId = departmentId,
            Priority = TaskPriority.Medium,
            StartDate = Clock.UtcNow.UtcDateTime,
            Deadline = deadline,
            EstimatedHours = 1,
            Status = status,
            CreatedAt = Clock.UtcNow.UtcDateTime,
            UpdatedAt = Clock.UtcNow.UtcDateTime,
            IsDeleted = isDeleted
        };

        Db.Tasks.Add(task);
        Db.SaveChanges();

        return task;
    }

    public void AddRecipient(TaskEntity task, User user)
    {
        Db.TaskRecipients.Add(new TaskRecipient { TaskId = task.Id, UserId = user.Id });
        Db.SaveChanges();
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
