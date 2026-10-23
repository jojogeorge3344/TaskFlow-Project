using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Analytics;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Services;
using TaskFlow.Infrastructure.Settings;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

internal sealed class AnalyticsHarness : IAsyncDisposable
{
    private readonly DashboardServiceHarness _inner;

    private AnalyticsHarness(DashboardServiceHarness inner, AdminDashboardScope scope, AdminAnalyticsService service, TaskService tasks)
    {
        _inner = inner;
        Tasks = tasks;
        Scope = scope;
        Service = service;
    }

    public AppDbContext Db => _inner.Db;

    public FakeTimeProvider Clock => _inner.Clock;

    public AdminDashboardScope Scope { get; }

    public AdminAnalyticsService Service { get; }

    public TaskService Tasks { get; }

    public DateTime Now => Clock.UtcNow.UtcDateTime;

    public static AnalyticsHarness Create(DateTimeOffset? now = null, string timezone = "UTC")
    {
        var inner = DashboardServiceHarness.Create(now, timezone);
        var appClock = new AppClock(
            inner.Db,
            inner.Clock,
            Options.Create(new ApplicationOptions { Timezone = timezone }),
            NullLogger<AppClock>.Instance);
        var scope = new AdminDashboardScope(inner.Db, appClock);
        var tasks = new TaskService(
            inner.Db,
            new TaskActivityService(inner.Db, inner.Clock),
            new NotificationPublisher(inner.Db, inner.Clock),
            inner.Clock,
            new ReminderSettingsService(inner.Db, inner.Clock, Options.Create(new ApplicationOptions())),
            appClock);
        return new AnalyticsHarness(inner, scope, new AdminAnalyticsService(inner.Db, scope, appClock), tasks);
    }

    public Project AddProject(string name) => _inner.AddProject(name);

    public User AddUser(
        string username,
        string role = SystemRoles.Employee,
        int? departmentId = null,
        int? managerId = null,
        bool isActive = true)
    {
        var user = _inner.AddUser(username, isActive, role);
        user.DepartmentId = departmentId;
        user.ManagerId = managerId;
        Db.SaveChanges();
        return user;
    }

    public Department AddDepartment(string name, params User[] managers)
    {
        var department = new Department { Name = name, CreatedAt = Now };
        Db.Departments.Add(department);
        Db.SaveChanges();
        foreach (var manager in managers)
        {
            Db.DepartmentManagers.Add(new DepartmentManager { ManagerUserId = manager.Id, DepartmentId = department.Id });
        }

        Db.SaveChanges();
        return department;
    }

    public TaskEntity AddTask(
        User assignee,
        TaskStatus status = TaskStatus.ToDo,
        DateTime? deadline = null,
        DateTime? createdAt = null,
        DateTime? completedAt = null,
        TaskPriority priority = TaskPriority.Medium,
        int? projectId = null,
        bool isDeleted = false)
    {
        var task = new TaskEntity
        {
            TaskCode = $"T{Guid.NewGuid():N}"[..8],
            Title = "Task",
            AssignedToUserId = assignee.Id,
            CreatedByUserId = assignee.Id,
            ProjectId = projectId,
            Priority = priority,
            StartDate = createdAt ?? Now,
            Deadline = deadline ?? Now.AddDays(5),
            EstimatedHours = 1,
            Status = status,
            CreatedAt = createdAt ?? Now,
            UpdatedAt = Now,
            CompletedAt = completedAt ?? (status == TaskStatus.Completed ? Now : null),
            IsDeleted = isDeleted
        };

        Db.Tasks.Add(task);
        Db.SaveChanges();
        return task;
    }

    public void AddRecipient(TaskEntity task, User user) => _inner.AddRecipient(task, user);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
