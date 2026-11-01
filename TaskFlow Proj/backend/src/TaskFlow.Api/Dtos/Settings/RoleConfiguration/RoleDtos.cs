namespace TaskFlow.Api.Dtos.Settings.RoleConfiguration;

/// <summary>One catalogue entry. <see cref="Id"/> is 0 for always-on features, which are not stored as grants.
/// <see cref="ScopeGroup"/> entries share a selector; a higher <see cref="ScopeRank"/> includes the lower ones.</summary>
public record PermissionDto(
    int Id,
    string Key,
    string Name,
    string Description,
    string Group,
    bool IsAssignable,
    string Module,
    string Screen,
    string Section,
    string Action,
    string? Requires,
    string? ScopeGroup,
    int ScopeRank,
    bool AlwaysGranted);

/// <summary>A role with the keys of the permissions it holds. <see cref="UserCount"/> counts every account holding it.</summary>
public record RoleDto(int Id, string Name, bool IsSystem, int UserCount, IReadOnlyList<string> PermissionKeys);

/// <summary>A role as the employee form offers it: no permissions, so it can be shown to anyone who may assign roles.</summary>
public record RoleOptionDto(int Id, string Name, bool IsSystem);

public record SaveRoleRequest(string? Name, IReadOnlyList<string>? PermissionKeys);
