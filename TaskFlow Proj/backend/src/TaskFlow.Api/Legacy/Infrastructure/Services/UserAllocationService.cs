using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Users;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Persistence;

namespace TaskFlow.Infrastructure.Services;

public class UserAllocationService : IUserAllocationService
{
    private const int MaxTotal = IUserAllocationService.MaxTotalPercent;
    private const string PercentRangeMessage = "Allocation percentage must be a whole number from 1 to 100.";

    private readonly AppDbContext _dbContext;

    public UserAllocationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async System.Threading.Tasks.Task ValidateForUserAsync(
        IReadOnlyList<ProjectAllocationInput> inputs,
        CancellationToken cancellationToken)
    {
        if (inputs.Any(input => input.Percent < 1 || input.Percent > MaxTotal))
        {
            throw new InvalidOperationException(PercentRangeMessage);
        }

        if (inputs.GroupBy(input => input.ProjectId).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("A project can only appear once for a person.");
        }

        var total = inputs.Sum(input => input.Percent);
        if (total > MaxTotal)
        {
            throw new InvalidOperationException(
                $"Allocations total {total}%, which is over {MaxTotal}%. A person can be allocated at most {MaxTotal}% in total.");
        }

        await RequireProjectsAsync(inputs.Select(input => input.ProjectId), cancellationToken);
        await RequirePositionsAsync(inputs.Select(input => input.PositionId), cancellationToken);
    }

    public async System.Threading.Tasks.Task ValidateDepartmentsAsync(IReadOnlyList<int> departmentIds, CancellationToken cancellationToken)
    {
        var distinct = departmentIds.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return;
        }

        var found = await _dbContext.Departments.CountAsync(d => distinct.Contains(d.Id) && !d.IsDeleted, cancellationToken);
        if (found != distinct.Count)
        {
            throw new InvalidOperationException("Department does not exist.");
        }
    }

    public async System.Threading.Tasks.Task StageForUserAsync(
        User user,
        IReadOnlyList<ProjectAllocationInput>? allocations,
        IReadOnlyList<int>? departmentIds,
        CancellationToken cancellationToken)
    {
        if (allocations is not null)
        {
            var existing = user.Id == 0
                ? []
                : await _dbContext.UserProjectAllocations.Where(a => a.UserId == user.Id).ToListAsync(cancellationToken);

            var removed = existing.Where(row => allocations.All(input => input.ProjectId != row.ProjectId)).ToList();
            _dbContext.UserProjectAllocations.RemoveRange(removed);

            // Leaving a project also ends managing it, so the manager link goes with the flagged row.
            var managedRemoved = removed.Where(row => row.IsProjectManager).Select(row => row.ProjectId).ToList();
            if (managedRemoved.Count > 0)
            {
                _dbContext.ManagerProjects.RemoveRange(
                    await _dbContext.ManagerProjects
                        .Where(link => link.ManagerUserId == user.Id && managedRemoved.Contains(link.ProjectId))
                        .ToListAsync(cancellationToken));
            }

            foreach (var input in allocations)
            {
                var row = existing.FirstOrDefault(a => a.ProjectId == input.ProjectId);
                if (row is null)
                {
                    user.ProjectAllocations.Add(new UserProjectAllocation
                    {
                        ProjectId = input.ProjectId,
                        PositionId = input.PositionId,
                        AllocationPercent = input.Percent
                    });
                }
                else
                {
                    row.PositionId = input.PositionId;
                    row.AllocationPercent = input.Percent;
                }
            }
        }

        if (departmentIds is not null)
        {
            var wanted = departmentIds.Distinct().ToList();
            var existing = user.Id == 0
                ? []
                : await _dbContext.UserDepartments.Where(link => link.UserId == user.Id).ToListAsync(cancellationToken);

            _dbContext.UserDepartments.RemoveRange(existing.Where(link => !wanted.Contains(link.DepartmentId)));
            foreach (var departmentId in wanted.Where(id => existing.All(link => link.DepartmentId != id)))
            {
                user.DepartmentMemberships.Add(new UserDepartment { DepartmentId = departmentId });
            }
        }
    }

    public async System.Threading.Tasks.Task ReplaceProjectMembersAsync(
        int projectId,
        IReadOnlyList<ProjectMemberInput> members,
        CancellationToken cancellationToken)
    {
        if (members.Any(member => member.Percent < 1 || member.Percent > MaxTotal))
        {
            throw new InvalidOperationException(PercentRangeMessage);
        }

        if (members.GroupBy(member => member.UserId).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("A person can only appear once on a project.");
        }

        if (members.Count > 0 && !members.Any(member => member.IsManager))
        {
            throw new InvalidOperationException("Mark at least one person on the team as the project's manager.");
        }

        await RequirePositionsAsync(members.Select(member => member.PositionId), cancellationToken);

        // The check and the write share one transaction, so two assigners cannot both pass the cap for the same person.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var userIds = members.Select(member => member.UserId).ToList();
        var users = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id) && u.IsActive)
            .Select(u => new { u.Id, u.FullName, u.RoleId })
            .ToListAsync(cancellationToken);
        if (users.Count != userIds.Count)
        {
            throw new InvalidOperationException("One or more people do not exist or are not active.");
        }

        var notManagers = members
            .Where(member => member.IsManager)
            .Select(member => users.First(u => u.Id == member.UserId))
            .Where(u => u.RoleId != SystemRoles.ManagerId)
            .ToList();
        if (notManagers.Count > 0)
        {
            throw new InvalidOperationException(
                $"Only a person with the Manager role can manage a project: {string.Join(", ", notManagers.Select(u => u.FullName))}.");
        }

        var onOtherProjects = await _dbContext.UserProjectAllocations
            .Where(a => userIds.Contains(a.UserId) && a.ProjectId != projectId)
            .GroupBy(a => a.UserId)
            .Select(group => new { UserId = group.Key, Total = group.Sum(a => a.AllocationPercent) })
            .ToDictionaryAsync(row => row.UserId, row => row.Total, cancellationToken);

        foreach (var member in members)
        {
            var other = onOtherProjects.GetValueOrDefault(member.UserId);
            if (other + member.Percent > MaxTotal)
            {
                var name = users.First(u => u.Id == member.UserId).FullName;
                throw new InvalidOperationException(
                    $"{name} would be allocated {other + member.Percent}% in total. They already have {other}% on other projects, so {MaxTotal - other}% remains.");
            }
        }

        var existing = await _dbContext.UserProjectAllocations
            .Where(a => a.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        _dbContext.UserProjectAllocations.RemoveRange(existing.Where(row => userIds.All(id => id != row.UserId)));
        foreach (var member in members)
        {
            var row = existing.FirstOrDefault(a => a.UserId == member.UserId);
            if (row is null)
            {
                _dbContext.UserProjectAllocations.Add(new UserProjectAllocation
                {
                    UserId = member.UserId,
                    ProjectId = projectId,
                    PositionId = member.PositionId,
                    AllocationPercent = member.Percent,
                    IsProjectManager = member.IsManager
                });
            }
            else
            {
                row.PositionId = member.PositionId;
                row.AllocationPercent = member.Percent;
                row.IsProjectManager = member.IsManager;
            }
        }

        // The "manager oversees this project" links are kept in step with the flags, so they have one source.
        var managerIds = members.Where(member => member.IsManager).Select(member => member.UserId).ToList();
        var links = await _dbContext.ManagerProjects.Where(link => link.ProjectId == projectId).ToListAsync(cancellationToken);
        _dbContext.ManagerProjects.RemoveRange(links.Where(link => !managerIds.Contains(link.ManagerUserId)));
        foreach (var managerId in managerIds.Where(id => links.All(link => link.ManagerUserId != id)))
        {
            _dbContext.ManagerProjects.Add(new ManagerProject { ManagerUserId = managerId, ProjectId = projectId });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async System.Threading.Tasks.Task RequireProjectsAsync(IEnumerable<int> projectIds, CancellationToken cancellationToken)
    {
        var distinct = projectIds.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return;
        }

        var found = await _dbContext.Projects.CountAsync(p => distinct.Contains(p.Id) && !p.IsDeleted, cancellationToken);
        if (found != distinct.Count)
        {
            throw new InvalidOperationException("Project does not exist.");
        }
    }

    private async System.Threading.Tasks.Task RequirePositionsAsync(IEnumerable<int> positionIds, CancellationToken cancellationToken)
    {
        var distinct = positionIds.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return;
        }

        var found = await _dbContext.Positions.CountAsync(p => distinct.Contains(p.Id), cancellationToken);
        if (found != distinct.Count)
        {
            throw new InvalidOperationException("Position does not exist.");
        }
    }
}
