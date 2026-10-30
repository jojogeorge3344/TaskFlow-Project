namespace TaskFlow.Api.Common.Security;

/// <summary>
/// Turns the permission keys a role holds into what its users may actually do. Two things are added to the stored keys
/// (always-on features need no key: every signed-in user has them):
/// <list type="bullet">
/// <item>The finer permissions that replaced an older, broader key. A role still holding the old key keeps exactly the
/// access it had; once an administrator saves the role in the new screen the old key is dropped and the finer ones are
/// stored explicitly.</item>
/// <item>The lower levels of a scope selector, so Organization includes Team and Own, and View all tasks includes View my tasks.</item>
/// </list>
/// </summary>
public static class EffectivePermissions
{
    /// <summary>Old broad key -> the finer permissions it used to cover.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> LegacyChildren =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [PermissionKeys.LegacyDashboardAdmin] = [PermissionKeys.DashboardViewOrganization],
            [PermissionKeys.LegacyDashboardManager] = [PermissionKeys.DashboardViewTeam],
            [PermissionKeys.LegacyProjectsManage] =
            [
                PermissionKeys.ProjectsCreate, PermissionKeys.ProjectsEdit, PermissionKeys.ProjectsDelete,
                PermissionKeys.ProjectsMembers, PermissionKeys.PositionsCreate
            ],
            [PermissionKeys.LegacyDepartmentsManage] =
            [
                PermissionKeys.DepartmentsCreate, PermissionKeys.DepartmentsEdit, PermissionKeys.DepartmentsDelete,
                PermissionKeys.DepartmentsMembers
            ],
            [PermissionKeys.LegacyEmployeesManage] =
            [
                PermissionKeys.EmployeesCreate, PermissionKeys.EmployeesEdit, PermissionKeys.EmployeesStatus,
                PermissionKeys.EmployeesMfaReset
            ],
            [PermissionKeys.LegacySettingsAi] =
            [
                PermissionKeys.SettingsAiView, PermissionKeys.SettingsAiEdit, PermissionKeys.SettingsAiTest,
                PermissionKeys.SettingsAiRemoveModel
            ]
        };

    private static readonly IReadOnlyDictionary<string, PermissionDefinition> ByKey =
        PermissionCatalog.All.ToDictionary(definition => definition.Key);

    /// <summary>True for keys that only exist as grants a role already holds, no longer shown or assignable.</summary>
    public static bool IsLegacy(string key) => LegacyChildren.ContainsKey(key);

    public static IReadOnlySet<string> Expand(IEnumerable<string> heldKeys)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in heldKeys)
        {
            result.Add(key);

            if (LegacyChildren.TryGetValue(key, out var children))
            {
                result.UnionWith(children);
            }
        }

        foreach (var key in result.ToList())
        {
            if (ByKey.TryGetValue(key, out var definition) && definition.ScopeGroup is not null)
            {
                // Always-on permissions are handled separately and cannot be switched off.
                result.UnionWith(
                    PermissionCatalog.All
                        .Where(other => other.ScopeGroup == definition.ScopeGroup && other.ScopeRank < definition.ScopeRank && !other.AlwaysGranted)
                        .Select(other => other.Key));
            }
        }

        return result;
    }

    /// <summary>The held keys that an administrator sees and can change: legacy keys are dropped because their finer
    /// replacements are already in <paramref name="effective"/>.</summary>
    public static IReadOnlyList<string> Visible(IEnumerable<string> effective) =>
        effective.Where(key => ByKey.ContainsKey(key)).Order(StringComparer.Ordinal).ToList();
}
