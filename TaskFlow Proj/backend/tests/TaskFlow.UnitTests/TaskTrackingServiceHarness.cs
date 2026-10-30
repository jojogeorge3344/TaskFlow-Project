using TaskFlow.Api.Services.Communication.Notification;
using TaskFlow.Api.Services.Communication.Mail;
using TaskFlow.Api.Repositories.Settings.ApplicationSettings;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Services.Settings.ReminderSettings;
using TaskFlow.Api.Services.Settings.ApplicationSettings;
using TaskFlow.Api.Config;
using Microsoft.Data.Sqlite;
using TaskFlow.Api.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;
using TaskFlow.Infrastructure.Services;
using Task = System.Threading.Tasks.Task;
using TaskPriority = TaskFlow.Api.Models.Enums.TaskPriority;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

internal sealed class TaskTrackingServiceHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private TaskTrackingServiceHarness(SqliteConnection connection, AppDbContext db)
    {
        _connection = connection;
        Db = db;

        ActivityService = new TaskActivityService(db, TimeProvider.System);
        NotificationPublisher = TestServices.NotificationPublisher(db, TimeProvider.System);
        NotificationService = TestServices.Notifications(db);
        ReminderSettings = new ReminderSettingsService(new ApplicationSettingsRepository(db), new UnitOfWork(db), TimeProvider.System, Options.Create(new ApplicationOptions()));
        Clock = new AppClock(new ApplicationSettingsRepository(db), TimeProvider.System, Options.Create(new ApplicationOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<AppClock>.Instance);
        TaskService = new TaskService(db, ActivityService, NotificationPublisher, TimeProvider.System, ReminderSettings, Clock, new TaskFlow.Infrastructure.Analytics.ManagerDashboardScope(db, Clock), TestServices.Mail(db, TimeProvider.System, Clock), Microsoft.Extensions.Logging.Abstractions.NullLogger<TaskService>.Instance);
        CommentService = new TaskCommentService(db, ActivityService, NotificationPublisher, TimeProvider.System);
    }

    public AppDbContext Db { get; }

    public TaskActivityService ActivityService { get; }

    public NotificationPublisher NotificationPublisher { get; }

    public NotificationService NotificationService { get; }

    public ReminderSettingsService ReminderSettings { get; }

    public AppClock Clock { get; }

    public TaskService TaskService { get; }

    public TaskCommentService CommentService { get; }

    public static TaskTrackingServiceHarness Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        TestAccessControl.Seed(db);

        return new TaskTrackingServiceHarness(connection, db);
    }

    public Project AddProject(string? name = null)
    {
        var project = new Project { Name = name ?? $"Project {Guid.NewGuid():N}"[..20], CreatedAt = DateTime.UtcNow };
        Db.Projects.Add(project);
        Db.SaveChanges();
        return project;
    }

    public User AddUser(string username, string role = SystemRoles.Employee, bool isActive = true)
    {
        var user = new User
        {
            FullName = $"{username} full name",
            Username = username,
            Email = $"{username}@taskflow.com",
            RoleId = TestAccessControl.IdFor(role),
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Db.Users.Add(user);
        Db.SaveChanges();

        return user;
    }

    public TaskFlow.Api.Models.Entities.Task AddTask(
        int assignedToUserId,
        int createdByUserId,
        string title = "Seed task",
        TaskStatus status = TaskStatus.ToDo,
        DateTime? deadline = null,
        TaskPriority priority = TaskPriority.Medium)
    {
        var task = new TaskFlow.Api.Models.Entities.Task
        {
            TaskCode = $"SEED-{Guid.NewGuid():N}"[..24],
            Title = title,
            Description = "Seed description",
            AssignedToUserId = assignedToUserId,
            CreatedByUserId = createdByUserId,
            Priority = priority,
            StartDate = DateTime.UtcNow.AddDays(-1),
            Deadline = deadline ?? DateTime.UtcNow.AddDays(5),
            EstimatedHours = 8,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Db.Tasks.Add(task);
        Db.SaveChanges();

        return task;
    }

    public void AddRecipient(int taskId, int userId)
    {
        Db.TaskRecipients.Add(new TaskRecipient { TaskId = taskId, UserId = userId });
        Db.SaveChanges();
    }

    public void SetSetting(string key, string value)
    {
        var existing = Db.ApplicationSettings.SingleOrDefault(setting => setting.Key == key);
        if (existing is null)
        {
            Db.ApplicationSettings.Add(new ApplicationSetting
            {
                Key = key,
                Value = value,
                Category = "Test",
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = value;
        }

        Db.SaveChanges();
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}