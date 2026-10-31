using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Dtos.Management.ProjectManagement;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Repositories.Management.DepartmentManagement;
using TaskFlow.Api.Repositories.Management.EmployeeManagement;
using TaskFlow.Api.Repositories.Management.EmployeeManagement.Allocations;
using TaskFlow.Api.Repositories.Management.ProjectManagement;
using TaskFlow.Api.Repositories.Management.ProjectManagement.Positions;

namespace TaskFlow.Api.Services.Management.EmployeeManagement.Allocations;

public class UserAllocationService : IUserAllocationService
{
    private const int MaxTotal = IUserAllocationService.MaxTotalPercent;
    private const string PercentRangeMessage = "Allocation percentage must be a whole number from 1 to 100.";

    private readonly IUserAllocationRepository _allocations;
    private readonly IEmployeeRepository _employees;
    private readonly IProjectRepository _projects;
    private readonly IPositionRepository _positions;
    private readonly IDepartmentRepository _departments;
    private readonly IUnitOfWork _unitOfWork;

    public UserAllocationService(
        IUserAllocationRepository allocations,
        IEmployeeRepository employees,
        IProjectRepository projects,
        IPositionRepository positions,
        IDepartmentRepository departments,
        IUnitOfWork unitOfWork)
    {
        _allocations = allocations;
        _employees = employees;
        _projects = projects;
        _positions = positions;
        _departments = departments;
        _unitOfWork = unitOfWork;
    }

    public async System.Threading.Tasks.Task ValidateForUserAsync(
        IReadOnlyList<ProjectAllocationInput> inputs,
        CancellationToken cancellationToken)
    {
        if (inputs.Any(input => input.Percent < 1 || input.Percent > MaxTotal))
        {
            throw new BusinessRuleException(PercentRangeMessage);
        }

        if (inputs.GroupBy(input => input.ProjectId).Any(group => group.Count() > 1))
        {
            throw new BusinessRuleException("A project can only appear once for a person.");
        }

        var total = inputs.Sum(input => input.Percent);
        if (total > MaxTotal)
        {
            throw new BusinessRuleException(
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

        if (await _departments.CountActiveAsync(distinct, cancellationToken) != distinct.Count)
        {
            throw new BusinessRuleException("Department does not exist.");
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
                : await _allocations.GetForUserForUpdateAsync(user.Id, cancellationToken);

            var removed = existing.Where(row => allocations.All(input => input.ProjectId != row.ProjectId)).ToList();
            _allocations.RemoveRange(removed);

            // Leaving a project also ends managing it, so the manager link goes with the flagged row.
            var managedRemoved = removed.Where(row => row.IsProjectManager).Select(row => row.ProjectId).ToList();
            if (managedRemoved.Count > 0)
            {
                _allocations.RemoveManagerLinks(
                    await _allocations.GetManagerLinksForUserForUpdateAsync(user.Id, managedRemoved, cancellationToken));
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
                : await _employees.GetDepartmentMembershipsForUpdateAsync(user.Id, cancellationToken);

            _employees.RemoveDepartmentMemberships(existing.Where(link => !wanted.Contains(link.DepartmentId)).ToList());
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
            throw new BusinessRuleException(PercentRangeMessage);
        }

        if (members.GroupBy(member => member.UserId).Any(group => group.Count() > 1))
        {
            throw new BusinessRuleException("A person can only appear once on a project.");
        }

        if (members.Count > 0 && !members.Any(member => member.IsManager))
        {
            throw new BusinessRuleException("Mark at least one person on the team as the project's manager.");
        }

        await RequirePositionsAsync(members.Select(member => member.PositionId), cancellationToken);

        // The check and the write share one transaction, so two assigners cannot both pass the cap for the same person.
        await _unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                var userIds = members.Select(member => member.UserId).ToList();
                var users = await _employees.GetActiveByIdsAsync(userIds, token);
                if (users.Count != userIds.Count)
                {
                    throw new BusinessRuleException("One or more people do not exist or are not active.");
                }

                var notManagers = members
                    .Where(member => member.IsManager)
                    .Select(member => users.First(u => u.Id == member.UserId))
                    .Where(u => u.RoleId != SystemRoles.ManagerId)
                    .ToList();
                if (notManagers.Count > 0)
                {
                    throw new BusinessRuleException(
                        $"Only a person with the Manager role can manage a project: {string.Join(", ", notManagers.Select(u => u.FullName))}.");
                }

                var onOtherProjects = await _allocations.GetTotalsOnOtherProjectsAsync(userIds, projectId, token);
                foreach (var member in members)
                {
                    var other = onOtherProjects.GetValueOrDefault(member.UserId);
                    if (other + member.Percent > MaxTotal)
                    {
                        var name = users.First(u => u.Id == member.UserId).FullName;
                        throw new BusinessRuleException(
                            $"{name} would be allocated {other + member.Percent}% in total. They already have {other}% on other projects, so {MaxTotal - other}% remains.");
                    }
                }

                var existing = await _allocations.GetForProjectForUpdateAsync(projectId, token);
                _allocations.RemoveRange(existing.Where(row => userIds.All(id => id != row.UserId)).ToList());
                foreach (var member in members)
                {
                    var row = existing.FirstOrDefault(a => a.UserId == member.UserId);
                    if (row is null)
                    {
                        _allocations.Add(new UserProjectAllocation
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
                var links = await _allocations.GetManagerLinksForProjectForUpdateAsync(projectId, token);
                _allocations.RemoveManagerLinks(links.Where(link => !managerIds.Contains(link.ManagerUserId)).ToList());
                foreach (var managerId in managerIds.Where(id => links.All(link => link.ManagerUserId != id)))
                {
                    _allocations.AddManagerLink(new ManagerProject { ManagerUserId = managerId, ProjectId = projectId });
                }

                await _unitOfWork.SaveChangesAsync(token);
            },
            cancellationToken);
    }

    private async System.Threading.Tasks.Task RequireProjectsAsync(IEnumerable<int> projectIds, CancellationToken cancellationToken)
    {
        var distinct = projectIds.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return;
        }

        if (await _projects.CountActiveAsync(distinct, cancellationToken) != distinct.Count)
        {
            throw new BusinessRuleException("Project does not exist.");
        }
    }

    private async System.Threading.Tasks.Task RequirePositionsAsync(IEnumerable<int> positionIds, CancellationToken cancellationToken)
    {
        var distinct = positionIds.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return;
        }

        if (await _positions.CountAsync(distinct, cancellationToken) != distinct.Count)
        {
            throw new BusinessRuleException("Position does not exist.");
        }
    }
}
