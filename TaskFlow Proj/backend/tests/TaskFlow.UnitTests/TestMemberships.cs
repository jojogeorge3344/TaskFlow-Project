using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.UnitTests;

/// <summary>Seeds the project allocations, department memberships and manager links that used to be single columns on the user.
/// Test databases are built with EnsureCreated, so the seeded positions (Member is id 1) are already there.</summary>
public static class TestMemberships
{
    public const int MemberPositionId = 1;

    /// <summary>Puts the user on a project (100% by default, position Member). A null project does nothing.</summary>
    public static void Allocate(AppDbContext db, User user, int? projectId, int percent = 100, int positionId = MemberPositionId)
    {
        if (projectId is not int project)
        {
            return;
        }

        db.UserProjectAllocations.Add(new UserProjectAllocation
        {
            UserId = user.Id,
            ProjectId = project,
            AllocationPercent = percent,
            PositionId = positionId
        });
        db.SaveChanges();
    }

    /// <summary>Adds the user to a department. A null department does nothing.</summary>
    public static void JoinDepartment(AppDbContext db, User user, int? departmentId)
    {
        if (departmentId is not int department)
        {
            return;
        }

        db.UserDepartments.Add(new UserDepartment { UserId = user.Id, DepartmentId = department });
        db.SaveChanges();
    }

    /// <summary>Puts the user on a manager's team, which is how a user gets a manager now (they share a department the manager
    /// manages). Uses one private department per manager so it does not disturb the department a test set up itself.</summary>
    public static Department ReportTo(AppDbContext db, User user, int managerId)
    {
        var name = $"Team of manager {managerId}";
        var department = db.Departments.FirstOrDefault(d => d.Name == name);
        if (department is null)
        {
            department = new Department { Name = name, CreatedAt = DateTime.UtcNow };
            db.Departments.Add(department);
            db.SaveChanges();
            db.DepartmentManagers.Add(new DepartmentManager { ManagerUserId = managerId, DepartmentId = department.Id });
            db.SaveChanges();
        }

        JoinDepartment(db, user, department.Id);
        return department;
    }
}
