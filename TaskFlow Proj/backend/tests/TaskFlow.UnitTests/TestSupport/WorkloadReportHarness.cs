using TaskFlow.Api.Services.Reports;
using TaskFlow.Api.Repositories.Monitoring.EmployeeWorkload;
using TaskFlow.Api.Services.Monitoring.EmployeeWorkload;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Services.Settings.ApplicationSettings;
using TaskFlow.Api.Config;
using Microsoft.Data.Sqlite;
using TaskFlow.Api.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests.TestSupport;

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
        var appClock = new AppClock(new ApplicationSettingsRepository(db), clock, Options.Create(new ApplicationOptions { Timezone = timezone }), NullLogger<AppClock>.Instance);

        return new WorkloadReportHarness(
            connection,
            db,
            new WorkloadService(new WorkloadRepository(db), appClock),
            new ReportService(new TaskFlow.Api.Repositories.Reports.ReportRepository(db), appClock),
            clock);
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
        int? projectId = null,
        string? fullName = null)
    {
        var user = new User
        {
            FullName = fullName ?? $"{username} full name",
            Username = username,
            Email = $"{username}@taskflow.com",
            PasswordHash = "hash",
            RoleId = TestAccessControl.IdFor(role),
            IsActive = isActive,
            CreatedAt = Clock.UtcNow.UtcDateTime,
            UpdatedAt = Clock.UtcNow.UtcDateTime
        };

        Db.Users.Add(user);
        Db.SaveChanges();
        TestMemberships.Allocate(Db, user, projectId);

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
