namespace TaskFlow.Api.Common.Security;

/// <summary>Maps a permission key to the name of the authorization policy that requires it. The REST controllers and the
/// assistant both use these names, so they always agree.</summary>
public static class PermissionPolicy
{
    public const string Prefix = "perm:";

    public static string NameFor(string permissionKey) => Prefix + permissionKey;
}
