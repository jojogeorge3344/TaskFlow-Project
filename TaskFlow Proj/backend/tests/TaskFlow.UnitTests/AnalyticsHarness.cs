using TaskFlow.Api.Services.Communication.Notification;
using TaskFlow.Api.Services.Communication.Mail;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Services.Settings.ReminderSettings;
using TaskFlow.Api.Services.Settings.ApplicationSettings;
using TaskFlow.Api.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Security;
using TaskFlow.Infrastructure.Analytics;
using TaskFlow.Api.Persistence;
using TaskFlow.Infrastructure.Services;
using TaskEntity = TaskFlow.Api.Models.Entities.Task;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

internal sealed class AnalyticsHarness : IAsyncDisposable
{
    private readonly DashboardServiceHarness _inner;

    private AnalyticsHarness(DashboardServiceHarness inner, AdminDashboardScope scope, AdminAnalyticsService service, TaskService tasks, ManagerDashboardScope managerScope, ManagerAnalyticsService managerService, EmployeeAnalyticsService employeeService)
    {
        _inner = inner;
        ManagerScope = managerScope;
        ManagerService = managerService;
        EmployeeService = employeeService;
        Tasks = tasks;
        Scope = scope;
        Service = service;
    }

    public AppDbContext Db => _inner.Db;

    public FakeTimeProvider Clock => _inner.Clock;

    public AdminDashboardScope Scope { get; }

    public ManagerDashboardScope ManagerScope { get; }

    public ManagerAnalyticsService ManagerService { get; }

    public EmployeeAnalyticsService EmployeeService { get; }

    public AdminAnalyticsService Service { get; }

    public TaskService Tasks { get; }

    public DateTime Now => Clock.UtcNow.UtcDateTime;

    public static AnalyticsHarness Create(DateTimeOffset? now = null, string timezone = "UTC")
    {
        var inner = DashboardServiceHarness.Create(now, timezone);
        var appClock = new AppClock(
            new ApplicationSettingsRepository(inner.Db),
            inner.Clock,
            Options.Create(new ApplicationOptions { Timezone = timezone }),
            NullLogger<AppClock>.Instance);
        var scope = new AdminDashboardScope(inner.Db, appClock);
        var managerScope = new ManagerDashboardScope(inner.Db, appClock);
        var tasks = new TaskService(
            inner.Db,
            new TaskActivityService(inner.Db, inner.Clock),
            TestServices.NotificationPublisher(inner.Db, inner.Clock),
            inner.Clock,
            new ReminderSettingsService(new ApplicationSettingsRepository(inner.Db), new UnitOfWork(inner.Db), inner.Clock, Options.Create(new ApplicationOptions())),
            appClock,
            managerScope,
            TestServices.Mail(inner.Db, inner.Clock, appClock),
            NullLogger<TaskService>.Instance);
        return new AnalyticsHarness(
            inner,
            scope,
            new AdminAnalyticsService(inner.Db, scope, appClock),
            tasks,
            managerScope,
            new ManagerAnalyticsService(inner.Db, managerScope, appClock),
            new EmployeeAnalyticsService(inner.Db, new EmployeeDashboardScope(inner.Db), appClock));
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
        TestMemberships.JoinDepartment(Db, user, departmentId);
        if (managerId is int manager)
        {
            TestMemberships.ReportTo(Db, user, manager);
        }

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

    public void AddManagerProject(User manager, Project project)
    {
        Db.ManagerProjects.Add(new ManagerProject { ManagerUserId = manager.Id, ProjectId = project.Id });
        Db.SaveChanges();
    }

    public void AddRecipient(TaskEntity task, User user) => _inner.AddRecipient(task, user);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
