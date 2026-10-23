using Microsoft.Data.Sqlite;
using TaskFlow.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Services;
using TaskFlow.Infrastructure.Settings;
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

    public static DashboardServiceHarness Create(DateTimeOffset? now = null, string timezone = "UTC")
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        TestAccessControl.Seed(db);

        var clock = new FakeTimeProvider(now ?? new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var appClock = new AppClock(db, clock, Options.Create(new ApplicationOptions { Timezone = timezone }), NullLogger<AppClock>.Instance);
        var service = new DashboardService(db, appClock, new TaskFlow.Infrastructure.Analytics.ManagerDashboardScope(db, appClock));

        return new DashboardServiceHarness(connection, db, service, clock);
    }

    public Project AddProject(string name)
    {
        var project = new Project
        {
            Name = name,
            CreatedAt = Clock.UtcNow.UtcDateTime
        };

        Db.Projects.Add(project);
        Db.SaveChanges();

        return project;
    }

    public User AddUser(
        string username,
        bool isActive = true,
        string role = SystemRoles.Employee,
        int? projectId = null)
    {
        var user = new User
        {
            FullName = $"{username} full name",
            Username = username,
            Email = $"{username}@taskflow.com",
            PasswordHash = "hash",
            RoleId = TestAccessControl.IdFor(role),
            IsActive = isActive,
            ProjectId = projectId,
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
        int? projectId = null,
        bool isDeleted = false)
    {
        var task = new TaskEntity
        {
            TaskCode = $"T{Guid.NewGuid():N}"[..8],
            Title = "Task",
            AssignedToUserId = assignedToUserId,
            CreatedByUserId = createdByUserId ?? assignedToUserId,
            ProjectId = projectId,
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
