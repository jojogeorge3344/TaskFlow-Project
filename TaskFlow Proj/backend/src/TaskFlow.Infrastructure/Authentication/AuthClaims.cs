namespace TaskFlow.Infrastructure.Authentication;

public static class AuthClaims
{
    /// <summary>The user's security stamp at sign-in; compared with the stored stamp on every request.</summary>
    public const string SecurityStamp = "sstamp";

    /// <summary>One claim per permission key the user's role currently holds, added from the database on every request.</summary>
    public const string Permission = "permission";

    /// <summary>Present (value "true") while the user signed in with a temporary password and must change it first.</summary>
    public const string MustChangePassword = "must_change_password";
}
