using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Dtos.Monitoring.TaskManagement;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Security;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;
using TaskFlow.UnitTests.TestSupport;

namespace TaskFlow.UnitTests.Dashboard.Analytics.Scopes;

public class AnalyticsDrillDownTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Past = new(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Soon = new(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    private sealed record Fixture(
        AnalyticsHarness Harness,
        Department Engineering,
        Project Alpha,
        User Manager,
        User Dev,
        User Other);

    private static async System.Threading.Tasks.Task<int> ListTotalAsync(AnalyticsHarness harness, TaskListFilter filter)
    {
        var page = await harness.Tasks.ListAsync(
            filter with { PageSize = 100 },
            1,
            SystemRoles.Administrator,
            CancellationToken.None);
        return page.Total;
    }

    private static Fixture Seed()
    {
        var harness = AnalyticsHarness.Create(Now, "Asia/Kolkata");
        var manager = harness.AddUser("mgr", SystemRoles.Manager);
        var engineering = harness.AddDepartment("Engineering", manager);
        var alpha = harness.AddProject("Alpha");
        var beta = harness.AddProject("Beta");
        var dev = harness.AddUser("dev", departmentId: engineering.Id);
        var other = harness.AddUser("other", managerId: manager.Id);
        var gone = harness.AddUser("gone", departmentId: engineering.Id);

        var shared = harness.AddTask(dev, TaskStatus.InProgress, Soon, projectId: alpha.Id, priority: TaskPriority.High);
        harness.AddRecipient(shared, other);
        harness.AddTask(dev, TaskStatus.ToDo, Past, projectId: alpha.Id, priority: TaskPriority.Critical);
        harness.AddTask(dev, TaskStatus.Completed, Past, projectId: beta.Id, createdAt: Now.UtcDateTime.AddDays(-3));
        harness.AddTask(dev, TaskStatus.Blocked, Later);
        harness.AddTask(other, TaskStatus.InQA, Soon, projectId: beta.Id);
        harness.AddTask(other, TaskStatus.ToDo, Now.UtcDateTime.AddHours(2), priority: TaskPriority.Low);
        harness.AddTask(other, TaskStatus.Cancelled, Past, projectId: alpha.Id);
        harness.AddTask(other, TaskStatus.ToDo, Soon, isDeleted: true);
        harness.AddTask(gone, TaskStatus.ToDo, Soon, projectId: alpha.Id);
        gone.IsDeleted = true;
        harness.Db.SaveChanges();

        return new Fixture(harness, engineering, alpha, manager, dev, other);
    }

    private static IEnumerable<AnalyticsFilter> Filters(Fixture fixture) =>
    [
        new AnalyticsFilter(),
        new AnalyticsFilter(Basis: AnalyticsDateBasis.Deadline, From: new DateOnly(2026, 6, 1), To: new DateOnly(2026, 6, 30)),
        new AnalyticsFilter(DepartmentId: fixture.Engineering.Id),
        new AnalyticsFilter(ProjectId: fixture.Alpha.Id),
        new AnalyticsFilter(ManagerId: fixture.Manager.Id)
    ];

    private static TaskListFilter Carry(AnalyticsFilter filter, AnalyticsFilter resolved) => new(
        Bucket: TaskBucketFilter.All,
        DepartmentId: filter.DepartmentId,
        ProjectId: filter.ProjectId,
        EmployeeId: filter.EmployeeId,
        ManagerId: filter.ManagerId,
        From: resolved.From,
        To: resolved.To,
        Basis: filter.Basis);

    private static TaskBucketFilter ToFilter(TaskBucket bucket) => bucket switch
    {
        TaskBucket.Pending => TaskBucketFilter.Pending,
        TaskBucket.InProgress => TaskBucketFilter.InProgress,
        TaskBucket.Completed => TaskBucketFilter.Completed,
        _ => TaskBucketFilter.Overdue
    };

    private static IEnumerable<(TaskBucket Bucket, int Value)> Buckets(BucketCountsDto counts) =>
    [
        (TaskBucket.Pending, counts.Pending),
        (TaskBucket.InProgress, counts.InProgress),
        (TaskBucket.Completed, counts.Completed),
        (TaskBucket.Overdue, counts.Overdue)
    ];

    [Fact]
    public async System.Threading.Tasks.Task EveryChartSegmentOpensAListWithTheSameCount()
    {
        var fixture = Seed();
        await using var harness = fixture.Harness;

        foreach (var filter in Filters(fixture))
        {
            var result = await harness.Service.GetAsync(filter, CancellationToken.None);
            var resolved = filter with { From = result.Filter.From, To = result.Filter.To };
            var carried = Carry(filter, resolved);

            foreach (var (bucket, value) in Buckets(result.TasksByStatus))
            {
                Assert.Equal(value, await ListTotalAsync(harness, carried with { Bucket = ToFilter(bucket) }));
            }

            foreach (var department in result.ByDepartment)
            {
                foreach (var (bucket, value) in Buckets(department.Tasks))
                {
                    var drill = carried with { Bucket = ToFilter(bucket), DepartmentId = department.DepartmentId ?? 0 };
                    Assert.Equal(value, await ListTotalAsync(harness, drill));
                }
            }

            foreach (var project in result.ByProject)
            {
                foreach (var (bucket, value) in Buckets(project.Tasks))
                {
                    var drill = carried with { Bucket = ToFilter(bucket), ProjectId = project.ProjectId ?? 0 };
                    Assert.Equal(value, await ListTotalAsync(harness, drill));
                }
            }

            foreach (var employee in result.ByEmployee.Items)
            {
                foreach (var (bucket, value) in Buckets(employee.Tasks))
                {
                    var drill = carried with { Bucket = ToFilter(bucket), EmployeeId = employee.UserId };
                    Assert.Equal(value, await ListTotalAsync(harness, drill));
                }
            }

            foreach (var manager in result.ByManager)
            {
                foreach (var (bucket, value) in Buckets(manager.Tasks))
                {
                    var drill = carried with { Bucket = ToFilter(bucket), ManagerId = manager.ManagerId };
                    Assert.Equal(value, await ListTotalAsync(harness, drill));
                }
            }

            foreach (var priority in result.TasksByPriority)
            {
                var drill = carried with { Priority = Enum.Parse<TaskPriority>(priority.Priority) };
                Assert.Equal(priority.Count, await ListTotalAsync(harness, drill));
            }

            var deadlines = result.DeadlineBuckets;
            Assert.Equal(deadlines.Overdue, await ListTotalAsync(harness, carried with { Bucket = null, DeadlineBucket = DeadlineBucket.Overdue }));
            Assert.Equal(deadlines.DueToday, await ListTotalAsync(harness, carried with { Bucket = null, DeadlineBucket = DeadlineBucket.DueToday }));
            Assert.Equal(deadlines.Next7Days, await ListTotalAsync(harness, carried with { Bucket = null, DeadlineBucket = DeadlineBucket.Next7Days }));
            Assert.Equal(deadlines.Later, await ListTotalAsync(harness, carried with { Bucket = null, DeadlineBucket = DeadlineBucket.Later }));
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task TrendPointsOpenAListWithTheSameCount()
    {
        var fixture = Seed();
        await using var harness = fixture.Harness;
        var filter = new AnalyticsFilter(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 15), DepartmentId: fixture.Engineering.Id);

        var result = await harness.Service.GetAsync(filter, CancellationToken.None);

        Assert.Equal("day", result.Trend.Granularity);
        Assert.Contains(result.Trend.Points, point => point.Created > 0);
        Assert.Contains(result.Trend.Points, point => point.Completed > 0);
        foreach (var point in result.Trend.Points)
        {
            var day = point.Date;
            var created = new TaskListFilter(
                Bucket: TaskBucketFilter.All,
                DepartmentId: fixture.Engineering.Id,
                From: day,
                To: day,
                Basis: AnalyticsDateBasis.Created);
            var completed = created with { Basis = AnalyticsDateBasis.Completed };

            Assert.Equal(point.Created, await ListTotalAsync(harness, created));
            Assert.Equal(point.Completed, await ListTotalAsync(harness, completed));
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task ASingleDateBoundIsOpenEndedAndAnInvertedRangeIsRejected()
    {
        var fixture = Seed();
        await using var harness = fixture.Harness;
        var all = await ListTotalAsync(harness, new TaskListFilter(Bucket: TaskBucketFilter.All));
        var created = Now.UtcDateTime;
        var day = DateOnly.FromDateTime(created);

        Assert.Equal(all, await ListTotalAsync(harness, new TaskListFilter(Bucket: TaskBucketFilter.All, From: day.AddDays(-10))));
        Assert.Equal(all, await ListTotalAsync(harness, new TaskListFilter(Bucket: TaskBucketFilter.All, To: day.AddDays(1))));
        Assert.True(await ListTotalAsync(harness, new TaskListFilter(Bucket: TaskBucketFilter.All, From: day.AddDays(1))) < all);
        await Assert.ThrowsAsync<AnalyticsFilterException>(() =>
            ListTotalAsync(harness, new TaskListFilter(From: day, To: day.AddDays(-1))));
    }
}
