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
using TaskPriority = TaskFlow.Domain.Enums.TaskPriority;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

internal sealed class WorkloadReportHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private WorkloadReportHarness(
        SqliteConnection connection,
        AppDbContext db,
        WorkloadService workload,
        ReportService reports,
        FakeTimeProvider clock)
    {
        _connection = connection;
        Db = db;
        Workload = workload;
        Reports = reports;
        Clock = clock;
    }

    public AppDbContext Db { get; }

    public WorkloadService Workload { get; }

    public ReportService Reports { get; }

    public FakeTimeProvider Clock { get; }

    public static WorkloadReportHarness Create(DateTimeOffset? now = null, string timezone = "UTC")
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

        return new WorkloadReportHarness(
            connection,
            db,
            new WorkloadService(db, appClock),
            new ReportService(db, appClock),
            clock);
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
        string role = SystemRoles.Employee,
        int? departmentId = null,
        string? fullName = null)
    {
        var user = new User
        {
            FullName = fullName ?? $"{username} full name",
            Username = username,
            Email = $"{username}@taskflow.local",
            PasswordHash = "hash",
            RoleId = TestAccessControl.IdFor(role),
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
        TaskPriority priority = TaskPriority.Medium,
        int? createdByUserId = null,
        bool isDeleted = false)
    {
        var task = new TaskEntity
        {
            TaskCode = $"T{Guid.NewGuid():N}"[..8],
            Title = "Task",
            AssignedToUserId = assignedToUserId,
            CreatedByUserId = createdByUserId ?? assignedToUserId,
            Priority = priority,
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

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
