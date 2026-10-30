using Microsoft.AspNetCore.Identity;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using TaskFlow.Api.Dtos.Profile;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Interfaces;
using TaskFlow.Api.Repositories.Interfaces.Auth;
using TaskFlow.Api.Repositories.Interfaces.Management.EmployeeManagement;
using TaskFlow.Api.Repositories.Interfaces.Monitoring.TaskManagement;
using TaskFlow.Api.Repositories.Interfaces.Settings.RoleConfiguration;
using TaskFlow.Api.Services.Interfaces.Management.EmployeeManagement;
using TaskFlow.Api.Services.Interfaces.Management.EmployeeManagement.Allocations;

namespace TaskFlow.Api.Services.Management.EmployeeManagement;

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employees;
    private readonly IRoleRepository _roles;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITaskRepository _tasks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IUserAllocationService _allocations;

    public EmployeeService(
        IEmployeeRepository employees,
        IRoleRepository roles,
        IRefreshTokenRepository refreshTokens,
        ITaskRepository tasks,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher,
        IUserAllocationService allocations)
    {
        _employees = employees;
        _roles = roles;
        _refreshTokens = refreshTokens;
        _tasks = tasks;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _allocations = allocations;
    }

    public async Task<IReadOnlyCollection<UserDto>> GetAllAsync(UserListFilter? filter, CancellationToken cancellationToken) =>
        await _employees.GetListAsync(filter, cancellationToken);

    public Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken) => _employees.GetDtoAsync(id, cancellationToken);

    public Task<UserDto?> GetOwnAsync(int id, CancellationToken cancellationToken) => GetByIdAsync(id, cancellationToken);

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = RequireValidEmail(request.Email);
        var username = request.Username.Trim();

        if (await EmailTakenAsync(email, null, cancellationToken))
        {
            throw new BusinessRuleException("Email already exists.");
        }

        // Deleted users keep their username (the unique index still covers them), so they count here too.
        if (await _employees.UsernameTakenAsync(username, exceptUserId: null, cancellationToken))
        {
            throw new BusinessRuleException("Email or username already exists.");
        }

        if (request.ProjectAllocations is not null)
        {
            await _allocations.ValidateForUserAsync(request.ProjectAllocations, cancellationToken);
        }

        if (request.DepartmentIds is not null)
        {
            await _allocations.ValidateDepartmentsAsync(request.DepartmentIds, cancellationToken);
        }

        var role = await RequireRoleAsync(request.RoleId, cancellationToken);

        var user = new User
        {
            EmployeeCode = request.EmployeeCode?.Trim(),
            FullName = request.FullName.Trim(),
            Username = username,
            Email = email,
            RoleId = role.Id,
            Role = role,
            IsActive = request.IsActive,
            SecurityStamp = NewSecurityStamp(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        await _allocations.StageForUserAsync(user, request.ProjectAllocations, request.DepartmentIds, cancellationToken);
        _employees.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _employees.GetActiveWithRoleForUpdateAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var email = RequireValidEmail(request.Email);
        var username = request.Username.Trim();

        if (await EmailTakenAsync(email, id, cancellationToken))
        {
            throw new BusinessRuleException("Email already exists.");
        }

        if (await _employees.UsernameTakenAsync(username, id, cancellationToken))
        {
            throw new BusinessRuleException("Email or username already exists.");
        }

        if (request.ProjectAllocations is not null)
        {
            await _allocations.ValidateForUserAsync(request.ProjectAllocations, cancellationToken);
        }

        if (request.DepartmentIds is not null)
        {
            await _allocations.ValidateDepartmentsAsync(request.DepartmentIds, cancellationToken);
        }

        // An employee's password is never set here: the employee changes it from their own profile or account recovery,
        // both of which need the authenticator. Any password on the request is ignored.
        const bool passwordChanged = false;

        var role = await RequireRoleAsync(request.RoleId, cancellationToken);

        var usernameChanged = !string.Equals(user.Username, username, StringComparison.Ordinal);
        var roleChanged = user.RoleId != request.RoleId;

        if (roleChanged)
        {
            await EnsureAnotherActiveAdministratorAsync(
                user, "The only active Administrator cannot be moved to another role.", cancellationToken);
        }

        var now = DateTime.UtcNow;

        if (roleChanged && user.RoleId == SystemRoles.ManagerId)
        {
            // Someone who is no longer a Manager oversees no projects and has no reports.
            await _employees.ClearManagerDutiesAsync(user.Id, cancellationToken);
        }

        user.EmployeeCode = request.EmployeeCode?.Trim();
        user.FullName = request.FullName.Trim();
        user.Username = username;
        user.Email = email;
        user.RoleId = role.Id;
        user.Role = role;
        await _allocations.StageForUserAsync(user, request.ProjectAllocations, request.DepartmentIds, cancellationToken);
        user.UpdatedAt = now;

        // A role change ends sessions too, so a demoted user cannot keep the old role's access token.
        if (usernameChanged || passwordChanged || roleChanged)
        {
            await EndSessionsAsync(user, now, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserDto?> UpdateStatusAsync(int id, UserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await _employees.GetActiveWithRoleForUpdateAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (user.IsActive && !request.IsActive)
        {
            await EnsureAnotherActiveAdministratorAsync(
                user, "The only active Administrator cannot be deactivated.", cancellationToken);

            // Deactivation ends sessions for good, so reactivating later cannot revive an old refresh cookie.
            await EndSessionsAsync(user, now, cancellationToken);
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserDto?> UpdateOwnProfileAsync(int id, UpdateOwnProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _employees.GetActiveWithRoleForUpdateAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var email = RequireValidEmail(request.Email);

        // Null keeps the current number; an empty value clears it.
        var mobileNumber = request.MobileNumber is null
            ? user.MobileNumber
            : MobileNumberPolicy.Normalize(request.MobileNumber);
        if (mobileNumber is not null && !MobileNumberPolicy.IsValid(mobileNumber))
        {
            throw new BusinessRuleException(MobileNumberPolicy.InvalidMessage);
        }

        // Email is the only unique identity, so a number may be shared. What is refused is a second record that already holds
        // this exact email and number together.
        if (mobileNumber is not null && await _employees.EmailAndMobileTakenAsync(email, mobileNumber, id, cancellationToken))
        {
            throw new BusinessRuleException("A user with this email and mobile number already exists.");
        }

        if (await EmailTakenAsync(email, id, cancellationToken))
        {
            throw new BusinessRuleException("Email already exists.");
        }

        user.FullName = request.FullName!.Trim();
        user.Email = email;
        user.MobileNumber = mobileNumber;
        user.UpdatedAt = DateTime.UtcNow;

        // Role, project, username, employee code and status are deliberately untouched.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserDto?> ChangeOwnPasswordAsync(int id, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _employees.GetActiveWithRoleForUpdateAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword!);
        user.UpdatedAt = now;

        // The password change ends every session, including this one; the client signs in again.
        await EndSessionsAsync(user, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<bool> DeleteAsync(int id, int currentUserId, CancellationToken cancellationToken)
    {
        var user = await _employees.GetForUpdateAsync(id, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (user.Id == currentUserId)
        {
            throw new BusinessRuleException("You cannot delete your own account.");
        }

        await EnsureAnotherActiveAdministratorAsync(
            user, "The only active Administrator cannot be deleted.", cancellationToken);

        var unfinished = await _tasks.CountUnfinishedAssignedToAsync(id, cancellationToken);
        if (unfinished > 0)
        {
            throw new BusinessRuleException(
                $"{user.FullName} still has {unfinished} unfinished task{(unfinished == 1 ? string.Empty : "s")}. Reassign {(unfinished == 1 ? "it" : "them")} before deleting this employee.");
        }

        var now = DateTime.UtcNow;

        // Their reports have no manager any more, and a deleted manager oversees no projects.
        await _employees.ClearManagerDutiesAsync(id, cancellationToken);

        // The email and username stay on the row, so they remain reserved and history is never ambiguous.
        await EndSessionsAsync(user, now, cancellationToken);
        // Allocations and memberships stay on the row, so reports keep a deleted person's projects; the soft-delete filters
        // already keep them out of every total and list.
        user.IsActive = false;
        user.IsDeleted = true;
        user.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string RequireValidEmail(string? email)
    {
        var normalized = EmailPolicy.Normalize(email);
        if (!EmailPolicy.IsValid(normalized))
        {
            throw new BusinessRuleException(EmailPolicy.InvalidMessage);
        }

        return normalized;
    }

    /// <summary>The domain is fixed, so two addresses are the same person's mailbox when their local parts match.
    /// Deleted users still count: their address stays reserved so history is never ambiguous.</summary>
    private Task<bool> EmailTakenAsync(string normalizedEmail, int? exceptUserId, CancellationToken cancellationToken) =>
        _employees.EmailPrefixTakenAsync(EmailPolicy.LocalPart(normalizedEmail) + "@", exceptUserId, cancellationToken);

    /// <summary>Keeps at least one active Administrator, the only role that can manage roles.</summary>
    private async System.Threading.Tasks.Task EnsureAnotherActiveAdministratorAsync(User user, string message, CancellationToken cancellationToken)
    {
        if (user.RoleId != SystemRoles.AdministratorId || !user.IsActive)
        {
            return;
        }

        if (await _employees.CountOtherActiveAdministratorsAsync(user.Id, cancellationToken) == 0)
        {
            throw new BusinessRuleException(message);
        }
    }

    private async Task<Role> RequireRoleAsync(int roleId, CancellationToken cancellationToken) =>
        await _roles.FindAsync(roleId, cancellationToken)
        ?? throw new BusinessRuleException("Role does not exist.");

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Ends every existing session of the user: access tokens carrying the old stamp fail validation,
    /// and their refresh tokens are revoked so no new ones can be minted. Saved by the caller.
    /// </summary>
    private async System.Threading.Tasks.Task EndSessionsAsync(User user, DateTime now, CancellationToken cancellationToken)
    {
        user.SecurityStamp = NewSecurityStamp();

        foreach (var token in await _refreshTokens.GetActiveForUsersForUpdateAsync([user.Id], cancellationToken))
        {
            token.RevokedAt = now;
        }
    }
}
