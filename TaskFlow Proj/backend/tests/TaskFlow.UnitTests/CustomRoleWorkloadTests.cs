using TaskFlow.Domain.Security;

namespace TaskFlow.UnitTests;

/// <summary>The workload view lists every active user whose role cannot see all tasks, so a custom role is treated like
/// an employee unless it holds tasks.view.all.</summary>
public class CustomRoleWorkloadTests
{
    [Fact]
    public async Task WorkloadListsCustomRoleUsersWithoutViewAllAndOmitsThoseWithIt()
    {
        await using var harness = WorkloadReportHarness.Create();
        var limited = TestAccessControl.AddCustomRole(harness.Db, "Limited", PermissionKeys.TasksCreate);
        var orgWide = TestAccessControl.AddCustomRole(harness.Db, "Org Wide", PermissionKeys.TasksViewAll);
        harness.AddUser("wl-employee", fullName: "Employee Person");
        var limitedUser = harness.AddUser("wl-limited", fullName: "Limited Person");
        limitedUser.RoleId = limited.Id;
        var orgUser = harness.AddUser("wl-org", fullName: "Org Person");
        orgUser.RoleId = orgWide.Id;
        harness.AddUser("wl-manager", role: SystemRoles.Manager, fullName: "Manager Person");
        harness.Db.SaveChanges();

        var workload = await harness.Workload.GetWorkloadAsync(null, CancellationToken.None);

        Assert.Equal(["Employee Person", "Limited Person"], workload.Select(entry => entry.FullName));
    }
}
