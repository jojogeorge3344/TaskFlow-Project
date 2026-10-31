namespace TaskFlow.Api.Repositories.Settings.RoleConfiguration.Roles;

/// <summary>A role with how many accounts hold it and the permission keys it is granted, as stored.</summary>
public record RoleSummary(int Id, string Name, bool IsSystem, int UserCount, IReadOnlyList<string> PermissionKeys);
