using TaskFlow.Api.Dtos.Dashboard.Analytics;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Common.Security;
using TaskStatus = TaskFlow.Api.Models.Enums.TaskStatus;

namespace TaskFlow.UnitTests;

public class AdminAnalyticsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Past = new(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Future = new(2026, 6, 25, 9, 0, 0, DateTimeKind.Utc);

    private static Task<AdminAnalyticsDto> GetAsync(AnalyticsHarness harness, AnalyticsFilter? filter = null) =>
        harness.Service.GetAsync(filter ?? new AnalyticsFilter(), CancellationToken.None);

    [Fact]
    public async Task Summary_CountsPeopleStructureAndTaskBuckets()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var manager = harness.AddUser("mgr", SystemRoles.Manager);
        harness.AddUser("admin", SystemRoles.Administrator);
        var ann = harness.AddUser("ann");
        harness.AddUser("bob");
        harness.AddUser("gone", isActive: false);
        harness.AddDepartment("Engineering", manager);
        harness.AddDepartment("Sales");
        harness.AddProject("Alpha");

        harness.AddTask(ann, TaskStatus.ToDo, deadline: Future);
        harness.AddTask(ann, TaskStatus.InProgress, deadline: Future);
        harness.AddTask(ann, TaskStatus.InQA, deadline: Future);
        harness.AddTask(ann, TaskStatus.Completed, deadline: Past);
        harness.AddTask(ann, TaskStatus.Blocked, deadline: Past);
        harness.AddTask(ann, TaskStatus.Cancelled, deadline: Past);
        harness.AddTask(ann, TaskStatus.ToDo, deadline: Past, isDeleted: true);

        var result = await GetAsync(harness);

        Assert.Equal(2, result.Summary.TotalEmployees);
        Assert.Equal(1, result.Summary.TotalManagers);
        Assert.Equal(2, result.Summary.TotalDepartments);
        Assert.Equal(1, result.Summary.TotalProjects);
        Assert.Equal(new BucketCountsDto(1, 2, 1, 1), result.Summary.Tasks);
        Assert.Equal(5, result.Summary.TotalTasks);
        Assert.Equal(20, result.Summary.CompletionPercent);
        Assert.Equal(result.Summary.Tasks, result.TasksByStatus);
    }

    [Fact]
    public async Task Summary_EmptyDataGivesZerosAndNoDivisionError()
    {
        await using var harness = AnalyticsHarness.Create(Now);

        var result = await GetAsync(harness);

        Assert.Equal(0, result.Summary.TotalTasks);
        Assert.Equal(0, result.Summary.CompletionPercent);
        Assert.Equal(0, result.TasksByStatus.Total);
        Assert.Empty(result.ByDepartment);
        Assert.Empty(result.ByProject);
        Assert.Empty(result.ByEmployee.Items);
        Assert.Equal(0, result.ByEmployee.TotalEmployees);
        Assert.All(result.TasksByPriority, priority => Assert.Equal(0, priority.Count));
        Assert.All(result.Trend.Points, point => Assert.Equal(0, point.Created + point.Completed));
    }

    [Fact]
    public async Task Summary_EntityTotalsFollowFiltersThatNarrowThem()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var mgrA = harness.AddUser("mgra", SystemRoles.Manager);
        harness.AddUser("mgrb", SystemRoles.Manager);
        var engineering = harness.AddDepartment("Engineering", mgrA);
        harness.AddDepartment("Sales");
        harness.AddProject("Alpha");
        harness.AddProject("Beta");
        harness.AddUser("dev1", departmentId: engineering.Id);
        harness.AddUser("dev2", departmentId: engineering.Id);
        harness.AddUser("rep");

        var byDepartment = await GetAsync(harness, new AnalyticsFilter(DepartmentId: engineering.Id));
        var byManager = await GetAsync(harness, new AnalyticsFilter(ManagerId: mgrA.Id));

        Assert.Equal(1, byDepartment.Summary.TotalDepartments);
        Assert.Equal(2, byDepartment.Summary.TotalEmployees);
        Assert.Equal(2, byDepartment.Summary.TotalProjects);
        Assert.Equal(1, byManager.Summary.TotalManagers);
        Assert.Equal(2, byManager.Summary.TotalEmployees);
    }

    [Fact]
    public async Task Priority_ReturnsEveryPriorityIncludingZeros()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var user = harness.AddUser("u1");
        harness.AddTask(user, priority: TaskPriority.High);
        harness.AddTask(user, priority: TaskPriority.High);
        harness.AddTask(user, priority: TaskPriority.Low);

        var result = await GetAsync(harness);

        Assert.Equal(
            [("Low", 1), ("Medium", 0), ("High", 2), ("Critical", 0)],
            result.TasksByPriority.Select(priority => (priority.Priority, priority.Count)).ToArray());
    }

    [Fact]
    public async Task DeadlineBuckets_SplitOpenTasksAndSumToOpenCount()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var user = harness.AddUser("u1");
        harness.AddTask(user, TaskStatus.ToDo, deadline: new DateTime(2026, 6, 14, 9, 0, 0, DateTimeKind.Utc));
        harness.AddTask(user, TaskStatus.InProgress, deadline: new DateTime(2026, 6, 15, 18, 0, 0, DateTimeKind.Utc));
        harness.AddTask(user, TaskStatus.ToDo, deadline: new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc));
        harness.AddTask(user, TaskStatus.ToDo, deadline: new DateTime(2026, 6, 22, 23, 0, 0, DateTimeKind.Utc));
        harness.AddTask(user, TaskStatus.ToDo, deadline: new DateTime(2026, 6, 23, 0, 0, 0, DateTimeKind.Utc));
        harness.AddTask(user, TaskStatus.Completed, deadline: new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc));

        var result = await GetAsync(harness);

        Assert.Equal(new DeadlineBucketsDto(1, 1, 2, 1), result.DeadlineBuckets);
        var buckets = result.DeadlineBuckets;
        Assert.Equal(
            result.TasksByStatus.Open,
            buckets.Overdue + buckets.DueToday + buckets.Next7Days + buckets.Later);
    }

    [Fact]
    public async Task Trend_IsZeroFilledPerDayAndIgnoresTheDateBasis()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var user = harness.AddUser("u1");
        harness.AddTask(
            user,
            TaskStatus.Completed,
            deadline: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            createdAt: new DateTime(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc),
            completedAt: new DateTime(2026, 6, 13, 8, 0, 0, DateTimeKind.Utc));
        harness.AddTask(user, createdAt: new DateTime(2026, 6, 13, 9, 0, 0, DateTimeKind.Utc));

        var filter = new AnalyticsFilter(new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 14), AnalyticsDateBasis.Deadline);
        var result = await GetAsync(harness, filter);

        Assert.Equal("day", result.Trend.Granularity);
        Assert.Equal(5, result.Trend.Points.Count);
        Assert.Equal(
            [(10, 0, 0), (11, 1, 0), (12, 0, 0), (13, 1, 1), (14, 0, 0)],
            result.Trend.Points.Select(point => (point.Date.Day, point.Created, point.Completed)).ToArray());
        Assert.Equal(0, result.Summary.TotalTasks - result.TasksByStatus.Total);
    }

    [Fact]
    public async Task Trend_UsesWeeklyBucketsBeyond31Days()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var user = harness.AddUser("u1");
        harness.AddTask(user, createdAt: new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc));
        harness.AddTask(user, createdAt: new DateTime(2026, 6, 3, 9, 0, 0, DateTimeKind.Utc));

        var filter = new AnalyticsFilter(new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 15));
        var result = await GetAsync(harness, filter);

        Assert.Equal("week", result.Trend.Granularity);
        Assert.All(result.Trend.Points, point => Assert.Equal(DayOfWeek.Monday, point.Date.DayOfWeek));
        Assert.Equal(new DateOnly(2026, 4, 27), result.Trend.Points[0].Date);
        Assert.Equal(new DateOnly(2026, 6, 15), result.Trend.Points[^1].Date);
        Assert.Equal(2, result.Trend.Points.Sum(point => point.Created));
        Assert.Equal(8, result.Trend.Points.Count);
    }

    [Fact]
    public async Task Trend_DailyForExactly31Days()
    {
        await using var harness = AnalyticsHarness.Create(Now);

        var result = await GetAsync(harness, new AnalyticsFilter(new DateOnly(2026, 5, 16), new DateOnly(2026, 6, 15)));

        Assert.Equal("day", result.Trend.Granularity);
        Assert.Equal(31, result.Trend.Points.Count);
    }

    [Fact]
    public async Task Department_GroupsByAssigneeAndReconcilesWithNoDepartment()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var engineering = harness.AddDepartment("Engineering");
        var sales = harness.AddDepartment("Sales");
        var dev = harness.AddUser("dev", departmentId: engineering.Id);
        var loner = harness.AddUser("loner");
        harness.AddTask(dev, TaskStatus.ToDo, deadline: Future);
        harness.AddTask(dev, TaskStatus.Completed);
        harness.AddTask(dev, TaskStatus.ToDo, deadline: Past);
        harness.AddTask(loner, TaskStatus.InProgress, deadline: Future);

        var result = await GetAsync(harness);

        var eng = Assert.Single(result.ByDepartment, department => department.Name == "Engineering");
        Assert.Equal(new BucketCountsDto(1, 0, 1, 1), eng.Tasks);
        Assert.Equal(33, eng.Tasks.CompletionPercent);
        Assert.Equal(0, Assert.Single(result.ByDepartment, department => department.Name == "Sales").Tasks.Total);
        var none = Assert.Single(result.ByDepartment, department => department.DepartmentId is null);
        Assert.Equal("No department", none.Name);
        Assert.Equal(new BucketCountsDto(0, 1, 0, 0), none.Tasks);
        Assert.Equal(result.TasksByStatus.Total, result.ByDepartment.Sum(department => department.Tasks.Total));
        Assert.Equal(sales.Id, result.ByDepartment.Single(department => department.Name == "Sales").DepartmentId);
    }

    [Fact]
    public async Task Department_TasksOfDeletedAssigneesStillReconcile()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var engineering = harness.AddDepartment("Engineering");
        var dev = harness.AddUser("dev", departmentId: engineering.Id);
        harness.AddTask(dev, TaskStatus.ToDo, deadline: Future);
        dev.IsDeleted = true;
        harness.Db.SaveChanges();

        var result = await GetAsync(harness);

        Assert.Equal(1, result.TasksByStatus.Total);
        Assert.Equal(1, result.ByDepartment.Sum(department => department.Tasks.Total));
    }

    [Fact]
    public async Task Employee_CountsAssigneeAndRecipientsOnceEachAndOrdersByOpenWorkload()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var ann = harness.AddUser("ann");
        var bob = harness.AddUser("bob");
        var cat = harness.AddUser("cat");
        var shared = harness.AddTask(ann, TaskStatus.ToDo, deadline: Future);
        harness.AddRecipient(shared, bob);
        harness.AddRecipient(shared, ann);
        harness.AddTask(ann, TaskStatus.ToDo, deadline: Past);
        harness.AddTask(bob, TaskStatus.Completed);
        harness.AddTask(cat, TaskStatus.Completed);

        var result = await GetAsync(harness);

        Assert.Equal(3, result.ByEmployee.TotalEmployees);
        Assert.Equal(["ann full name", "bob full name", "cat full name"], result.ByEmployee.Items.Select(item => item.Name).ToArray());
        Assert.Equal(new BucketCountsDto(1, 0, 0, 1), result.ByEmployee.Items[0].Tasks);
        Assert.Equal(new BucketCountsDto(1, 0, 1, 0), result.ByEmployee.Items[1].Tasks);
        Assert.True(result.ByEmployee.Items.Sum(item => item.Tasks.Total) > result.TasksByStatus.Total);
    }

    [Fact]
    public async Task Employee_ShowsTopTenAndReportsTheTotal()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        for (var index = 1; index <= 12; index++)
        {
            var user = harness.AddUser($"user{index:00}");
            for (var count = 0; count < index; count++)
            {
                harness.AddTask(user, TaskStatus.ToDo, deadline: Future);
            }
        }

        var result = await GetAsync(harness);

        Assert.Equal(10, result.ByEmployee.Items.Count);
        Assert.Equal(12, result.ByEmployee.TotalEmployees);
        Assert.Equal(12, result.ByEmployee.Items[0].Tasks.Total);
        Assert.Equal(3, result.ByEmployee.Items[^1].Tasks.Total);
    }

    [Fact]
    public async Task Employee_AllEmployeesOptionReturnsEveryEmployee()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        for (var index = 1; index <= 12; index++)
        {
            harness.AddTask(harness.AddUser($"user{index:00}"), TaskStatus.ToDo, deadline: Future);
        }

        var top = await GetAsync(harness);
        var all = await GetAsync(harness, new AnalyticsFilter(AllEmployees: true));

        Assert.Equal(10, top.ByEmployee.Items.Count);
        Assert.Equal(12, all.ByEmployee.Items.Count);
        Assert.Equal(12, all.ByEmployee.TotalEmployees);
    }

    [Fact]
    public async Task Manager_ShowsTeamTotalsAndManagersWithoutATeam()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var mgr = harness.AddUser("mgr", SystemRoles.Manager);
        var idle = harness.AddUser("idle", SystemRoles.Manager);
        var department = harness.AddDepartment("Engineering", mgr);
        var project = harness.AddProject("Alpha");
        harness.Db.ManagerProjects.Add(new TaskFlow.Api.Models.Entities.ManagerProject { ManagerUserId = mgr.Id, ProjectId = project.Id });
        harness.Db.SaveChanges();
        var report = harness.AddUser("report", departmentId: department.Id);
        var member = harness.AddUser("member", departmentId: department.Id);
        var both = harness.AddUser("both", departmentId: department.Id);
        harness.AddTask(report, TaskStatus.ToDo, deadline: Future);
        harness.AddTask(member, TaskStatus.Completed);
        harness.AddTask(both, TaskStatus.InProgress, deadline: Past);

        var result = await GetAsync(harness);

        var team = Assert.Single(result.ByManager, manager => manager.ManagerId == mgr.Id);
        Assert.Equal(1, team.Departments);
        Assert.Equal(3, team.Employees);
        Assert.Equal(1, team.Projects);
        Assert.Equal(new BucketCountsDto(1, 0, 1, 1), team.Tasks);
        var none = Assert.Single(result.ByManager, manager => manager.ManagerId == idle.Id);
        Assert.Equal(0, none.Departments + none.Employees + none.Projects + none.Tasks.Total);
    }

    [Fact]
    public async Task Project_ShowsProgressOutstandingProjectedEndAndNoProjectGroup()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var user = harness.AddUser("u1");
        var alpha = harness.AddProject("Alpha");
        harness.AddProject("Empty");
        harness.AddTask(user, TaskStatus.Completed, deadline: Past, projectId: alpha.Id);
        harness.AddTask(user, TaskStatus.ToDo, deadline: Future, projectId: alpha.Id);
        harness.AddTask(user, TaskStatus.ToDo, deadline: Past, projectId: alpha.Id);
        harness.AddTask(user, TaskStatus.InProgress, deadline: Future);

        var result = await GetAsync(harness);

        var project = Assert.Single(result.ByProject, row => row.Name == "Alpha");
        Assert.Equal(new BucketCountsDto(1, 0, 1, 1), project.Tasks);
        Assert.Equal(33, project.Tasks.CompletionPercent);
        Assert.Equal(2, project.Outstanding);
        Assert.Equal(Future, project.ProjectedEnd);
        var empty = Assert.Single(result.ByProject, row => row.Name == "Empty");
        Assert.Equal(0, empty.Tasks.CompletionPercent);
        Assert.Null(empty.ProjectedEnd);
        var none = Assert.Single(result.ByProject, row => row.ProjectId is null);
        Assert.Equal("No project", none.Name);
        Assert.Equal(1, none.Tasks.Total);
        Assert.Equal(result.TasksByStatus.Total, result.ByProject.Sum(row => row.Tasks.Total));
    }

    [Fact]
    public async Task AllSections_ReconcileUnderCombinedFilters()
    {
        await using var harness = AnalyticsHarness.Create(Now);
        var engineering = harness.AddDepartment("Engineering");
        var dev = harness.AddUser("dev", departmentId: engineering.Id);
        var other = harness.AddUser("other");
        var alpha = harness.AddProject("Alpha");
        foreach (var status in Enum.GetValues<TaskStatus>())
        {
            harness.AddTask(dev, status, deadline: Past, projectId: alpha.Id);
            harness.AddTask(dev, status, deadline: Future);
            harness.AddTask(other, status, deadline: Past, projectId: alpha.Id);
        }

        foreach (var filter in new[]
                 {
                     new AnalyticsFilter(),
                     new AnalyticsFilter(DepartmentId: engineering.Id),
                     new AnalyticsFilter(ProjectId: alpha.Id),
                     new AnalyticsFilter(DepartmentId: engineering.Id, ProjectId: alpha.Id)
                 })
        {
            var result = await GetAsync(harness, filter);
            var total = result.TasksByStatus.Total;

            Assert.Equal(total, result.TasksByPriority.Sum(priority => priority.Count));
            Assert.Equal(total, result.ByDepartment.Sum(department => department.Tasks.Total));
            Assert.Equal(total, result.ByProject.Sum(project => project.Tasks.Total));
            var deadlines = result.DeadlineBuckets;
            Assert.Equal(
                result.TasksByStatus.Open,
                deadlines.Overdue + deadlines.DueToday + deadlines.Next7Days + deadlines.Later);
        }
    }

    [Fact]
    public async Task FilterOptions_ListsEntitiesAndToday()
    {
        await using var harness = AnalyticsHarness.Create(Now, "Asia/Kolkata");
        harness.AddUser("mgr", SystemRoles.Manager);
        harness.AddUser("ann");
        harness.AddDepartment("Engineering");
        harness.AddProject("Alpha");

        var options = await harness.Service.GetFilterOptionsAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 6, 15), options.Today);
        Assert.Equal("Asia/Kolkata", options.Timezone);
        Assert.Single(options.Departments);
        Assert.Single(options.Projects);
        Assert.Equal(2, options.Employees.Count);
        Assert.Single(options.Managers);
    }
}
