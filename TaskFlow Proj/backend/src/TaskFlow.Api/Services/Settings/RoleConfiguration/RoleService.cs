using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Settings.RoleConfiguration;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Repositories.Interfaces;
using TaskFlow.Api.Repositories.Interfaces.Auth;
using TaskFlow.Api.Repositories.Interfaces.Management.EmployeeManagement;
using TaskFlow.Api.Repositories.Interfaces.Settings.RoleConfiguration;
using TaskFlow.Api.Services.Interfaces.Settings.RoleConfiguration;

namespace TaskFlow.Api.Services.Settings.RoleConfiguration;

/// <summary>Manages custom roles. System roles and the permission catalog are read-only here, and the database refuses
/// changes to them as well.</summary>
public class RoleService : IRoleService
{
    public const int MaxNameLength = 100;

    private readonly IRoleRepository _roles;
    private readonly IPermissionRepository _permissions;
    private readonly IEmployeeRepository _employees;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public RoleService(
        IRoleRepository roles,
        IPermissionRepository permissions,
        IEmployeeRepository employees,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _roles = roles;
        _permissions = permissions;
        _employees = employees;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _roles.GetSummariesAsync(cancellationToken);

        return roles
            .Select(role => new RoleDto(
                role.Id, role.Name, role.IsSystem, role.UserCount, EffectivePermissions.Visible(EffectivePermissions.Expand(role.PermissionKeys))))
            .ToList();
    }

    public async Task<RoleDto> GetRoleAsync(int id, CancellationToken cancellationToken) =>
        (await GetRolesAsync(cancellationToken)).FirstOrDefault(role => role.Id == id)
        ?? throw new RoleException(RoleFailure.NotFound, "That role does not exist.");

    public async Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken)
    {
        var ids = await _permissions.GetIdsByKeyAsync(cancellationToken);

        // The hierarchy lives in the catalogue; the database supplies the ids roles are granted by. Superseded keys that
        // only survive as old grants are not part of the catalogue and are not listed.
        return PermissionCatalog.All
            .Select(definition => new PermissionDto(
                ids.GetValueOrDefault(definition.Key),
                definition.Key,
                definition.Name,
                definition.Description,
                definition.Group,
                definition.IsAssignable,
                definition.Module,
                definition.Screen,
                definition.Section,
                definition.Action,
                definition.Requires,
                definition.ScopeGroup,
                definition.ScopeRank,
                definition.AlwaysGranted))
            .ToList();
    }

    public async Task<RoleDto> CreateAsync(SaveRoleRequest request, CancellationToken cancellationToken)
    {
        var name = ValidateName(request.Name);
        var permissions = await ResolvePermissionsAsync(request.PermissionKeys, cancellationToken);
        await EnsureNameFreeAsync(name, exceptRoleId: null, cancellationToken);

        var role = new Role { Name = name, IsSystem = false, CreatedAt = _timeProvider.GetUtcNow().UtcDateTime };
        foreach (var permission in permissions)
        {
            role.Permissions.Add(new RolePermission { Permission = permission });
        }

        _roles.Add(role);
        await SaveAsync(cancellationToken);

        return await GetRoleAsync(role.Id, cancellationToken);
    }

    public async Task<RoleDto> UpdateAsync(int id, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await LoadCustomRoleAsync(id, cancellationToken);
        var name = ValidateName(request.Name);
        var permissions = await ResolvePermissionsAsync(request.PermissionKeys, cancellationToken);
        await EnsureNameFreeAsync(name, id, cancellationToken);

        role.Name = name;

        var permissionsChanged = false;
        var wanted = permissions.Select(permission => permission.Id).ToHashSet();
        foreach (var grant in role.Permissions.Where(grant => !wanted.Contains(grant.PermissionId)).ToList())
        {
            role.Permissions.Remove(grant);
            permissionsChanged = true;
        }

        var held = role.Permissions.Select(grant => grant.PermissionId).ToHashSet();
        foreach (var permission in permissions.Where(permission => !held.Contains(permission.Id)))
        {
            role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            permissionsChanged = true;
        }

        if (permissionsChanged)
        {
            await EndSessionsOfRoleHoldersAsync(id, cancellationToken);
        }

        await SaveAsync(cancellationToken);

        return await GetRoleAsync(id, cancellationToken);
    }

    public async System.Threading.Tasks.Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var role = await LoadCustomRoleAsync(id, cancellationToken);

        var users = await _employees.CountHoldingRoleAsync(id, cancellationToken);
        if (users > 0)
        {
            throw new RoleException(
                RoleFailure.InUse,
                users == 1
                    ? "1 user still holds this role. Move them to another role first."
                    : $"{users} users still hold this role. Move them to another role first.");
        }

        _roles.Remove(role);
        await SaveAsync(cancellationToken);
    }

    /// <summary>Signs out everyone holding the role: a new security stamp fails their access tokens on the next request and
    /// revoking the refresh tokens stops new ones being minted. Saved by the caller.</summary>
    private async System.Threading.Tasks.Task EndSessionsOfRoleHoldersAsync(int roleId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var holders = await _employees.GetHoldingRoleForUpdateAsync(roleId, cancellationToken);
        foreach (var holder in holders)
        {
            holder.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        var tokens = await _refreshTokens.GetActiveForUsersForUpdateAsync(holders.Select(user => user.Id).ToList(), cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }
    }

    private async Task<Role> LoadCustomRoleAsync(int id, CancellationToken cancellationToken)
    {
        var role = await _roles.GetWithPermissionsForUpdateAsync(id, cancellationToken)
            ?? throw new RoleException(RoleFailure.NotFound, "That role does not exist.");

        if (role.IsSystem)
        {
            throw new RoleException(RoleFailure.ReadOnly, $"{role.Name} is a system role and is read-only: it cannot be changed or deleted.");
        }

        return role;
    }

    private static string ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new RoleException(RoleFailure.Invalid, "Enter a name for the role.");
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw new RoleException(RoleFailure.Invalid, $"The role name can be at most {MaxNameLength} characters.");
        }

        return trimmed;
    }

    private async System.Threading.Tasks.Task EnsureNameFreeAsync(string name, int? exceptRoleId, CancellationToken cancellationToken)
    {
        // The Name column compares case-insensitively, so this also catches "team lead" when "Team Lead" exists.
        if (await _roles.NameTakenAsync(name, exceptRoleId, cancellationToken))
        {
            throw new RoleException(RoleFailure.Duplicate, $"A role named \"{name}\" already exists.");
        }
    }

    private async Task<IReadOnlyList<Permission>> ResolvePermissionsAsync(IReadOnlyList<string>? keys, CancellationToken cancellationToken)
    {
        var requested = (keys ?? []).Where(key => !string.IsNullOrWhiteSpace(key)).Select(key => key.Trim()).Distinct().ToList();

        // An old broad key means the finer permissions that replaced it. Always-on features are never stored.
        var wanted = requested
            .SelectMany(key => EffectivePermissions.LegacyChildren.TryGetValue(key, out var children) ? children : [key])
            .Distinct()
            .Where(key => !PermissionCatalog.All.Any(definition => definition.Key == key && definition.AlwaysGranted))
            .ToList();
        var found = await _permissions.GetByKeysAsync(wanted, cancellationToken);

        var unknown = wanted.Except(found.Select(permission => permission.Key)).ToList();
        if (unknown.Count > 0)
        {
            throw new RoleException(RoleFailure.Invalid, $"Unknown permission: {string.Join(", ", unknown)}.");
        }

        var effective = EffectivePermissions.Expand(wanted);
        foreach (var permission in found)
        {
            var definition = PermissionCatalog.All.FirstOrDefault(definition => definition.Key == permission.Key);
            var requires = definition?.Requires;
            if (requires is not null && !effective.Contains(requires))
            {
                var needed = PermissionCatalog.All.FirstOrDefault(definition => definition.Key == requires)?.Name ?? requires;
                throw new RoleException(RoleFailure.Invalid, $"\"{permission.Name}\" also needs \"{needed}\".");
            }
        }

        // The catalogue decides, not the stored row: rows of the permission table can never be updated.
        var notAssignable = found
            .Where(permission =>
            {
                var definition = PermissionCatalog.All.FirstOrDefault(definition => definition.Key == permission.Key);
                return definition is { IsAssignable: false };
            })
            .Select(permission => permission.Name)
            .ToList();
        if (notAssignable.Count > 0)
        {
            throw new RoleException(
                RoleFailure.Invalid,
                $"{string.Join(", ", notAssignable)} can only be held by the Administrator role and cannot be given to another role.");
        }

        return found;
    }

    private async System.Threading.Tasks.Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // Two administrators saving the same name at once: the unique index decides.
            throw new RoleException(RoleFailure.Duplicate, "A role with that name already exists.");
        }
    }
}
