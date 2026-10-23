using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Dashboard.Analytics;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Security;
using TaskFlow.Infrastructure.Analytics;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Settings;
using TaskEntity = TaskFlow.Domain.Entities.Task;
using TaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class AdminAnalyticsQueryShapeTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public void Reset() => Count = 0;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static (SqliteConnection Connection, AppDbContext Db, AdminAnalyticsService Service, CommandCounter Counter) Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var counter = new CommandCounter();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(counter).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        TestAccessControl.Seed(db);

        var clock = new AppClock(
            db,
            new FakeTimeProvider(Now),
            Options.Create(new ApplicationOptions { Timezone = "UTC" }),
            NullLogger<AppClock>.Instance);
        var service = new AdminAnalyticsService(db, new AdminDashboardScope(db, clock), clock);
        return (connection, db, service, counter);
    }

    private static void Seed(AppDbContext db, string tag, int departments, int managers, int users, int projects, int tasks)
    {
        var now = Now.UtcDateTime;
        for (var index = 1; index <= departments; index++)
        {
            db.Departments.Add(new Department { Name = $"{tag}Dept {index}", CreatedAt = now });
        }

        for (var index = 1; index <= projects; index++)
        {
            db.Projects.Add(new Project { Name = $"{tag}Project {index}", CreatedAt = now });
        }

        db.SaveChanges();

        var departmentIds = db.Departments.Select(department => department.Id).ToList();
        var projectIds = db.Projects.Select(project => project.Id).ToList();

        for (var index = 1; index <= managers; index++)
        {
            db.Users.Add(NewUser($"{tag}mgr{index}", SystemRoles.ManagerId, departmentIds[index % departmentIds.Count], null, now));
        }

        db.SaveChanges();
        var managerIds = db.Users.Where(user => user.RoleId == SystemRoles.ManagerId && user.Username.StartsWith(tag)).Select(user => user.Id).ToList();
        // A manager's team is the people in the departments they manage, so each manager manages the department they sit in.
        foreach (var link in db.UserDepartments.Where(m => managerIds.Contains(m.UserId)).Select(m => new { m.UserId, m.DepartmentId }).ToList())
        {
            db.DepartmentManagers.Add(new DepartmentManager { ManagerUserId = link.UserId, DepartmentId = link.DepartmentId });
        }

        db.SaveChanges();

        for (var index = 1; index <= users; index++)
        {
            db.Users.Add(NewUser(
                $"{tag}emp{index}",
                SystemRoles.EmployeeId,
                departmentIds[index % departmentIds.Count],
                managerIds[index % managerIds.Count],
                now));
        }

        db.SaveChanges();
        var userIds = db.Users.Where(user => user.RoleId == SystemRoles.EmployeeId && user.Username.StartsWith(tag)).Select(user => user.Id).ToList();
        var statuses = Enum.GetValues<TaskStatus>();

        for (var index = 0; index < tasks; index++)
        {
            var created = now.AddDays(-(index % 60));
            var status = statuses[index % statuses.Length];
            db.Tasks.Add(new TaskEntity
            {
                TaskCode = $"{tag}{index:D6}",
                Title = "Task",
                AssignedToUserId = userIds[index % userIds.Count],
                CreatedByUserId = userIds[0],
                ProjectId = index % 7 == 0 ? null : projectIds[index % projectIds.Count],
                Priority = (TaskPriority)(index % 4),
                StartDate = created,
                Deadline = created.AddDays(index % 20 - 5),
                EstimatedHours = 1,
                Status = status,
                CreatedAt = created,
                UpdatedAt = created,
                CompletedAt = status == TaskStatus.Completed ? created.AddDays(1) : null
            });
        }

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    private static User NewUser(string username, int roleId, int departmentId, int? managerId, DateTime now) => new()
    {
        FullName = username,
        Username = username,
        Email = $"{username}@taskflow.com",
        PasswordHash = "hash",
        SecurityStamp = "stamp",
        RoleId = roleId,
        DepartmentMemberships = { new UserDepartment { DepartmentId = departmentId } },
        CreatedAt = now,
        UpdatedAt = now
    };

    [Fact]
    public async System.Threading.Tasks.Task QueryCount_DoesNotGrowWithDataVolume()
    {
        var (connection, db, service, counter) = Create();
        await using var _ = db;
        await using var __ = connection;

        Seed(db, "a", 3, 2, 10, 3, 60);
        counter.Reset();
        await service.GetAsync(new AnalyticsFilter(), CancellationToken.None);
        var small = counter.Count;

        Seed(db, "b", 20, 15, 300, 25, 2000);
        counter.Reset();
        await service.GetAsync(new AnalyticsFilter(), CancellationToken.None);
        var large = counter.Count;

        Assert.True(small > 0);
        Assert.Equal(small, large);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async System.Threading.Tasks.Task Timing_FullAnalyticsOver30kTasksStaysUnderTheBudget()
    {
        var (connection, db, service, _) = Create();
        await using var _db = db;
        await using var _connection = connection;

        Seed(db, "c", 20, 15, 300, 25, 30000);
        var filter = new AnalyticsFilter(Basis: AnalyticsDateBasis.Deadline, From: new DateOnly(2026, 4, 1), To: new DateOnly(2026, 6, 15));
        await service.GetAsync(filter, CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        var result = await service.GetAsync(filter, CancellationToken.None);
        stopwatch.Stop();

        Assert.True(result.TasksByStatus.Total > 0);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 5000,
            $"Analytics over 30k tasks took {stopwatch.ElapsedMilliseconds} ms.");
    }
}
