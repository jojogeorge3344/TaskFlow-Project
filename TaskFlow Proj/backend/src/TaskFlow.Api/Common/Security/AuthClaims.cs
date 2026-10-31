namespace TaskFlow.Api.Common.Security;

public static class AuthClaims
{
    /// <summary>The user's security stamp at sign-in; compared with the stored stamp on every request.</summary>
    public const string SecurityStamp = "sstamp";

    /// <summary>One claim per permission key the user's role currently holds, added from the database on every request.</summary>
    public const string Permission = "permission";

    /// <summary>Present while MFA is required and the user has not set it up; the gate lets only the setup endpoints through.</summary>
    public const string MfaSetupRequired = "mfa_setup_required";
}
