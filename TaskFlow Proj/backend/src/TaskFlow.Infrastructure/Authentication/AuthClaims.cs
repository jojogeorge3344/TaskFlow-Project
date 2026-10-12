namespace TaskFlow.Infrastructure.Authentication;

public static class AuthClaims
{
    /// <summary>The user's security stamp at sign-in; compared with the stored stamp on every request.</summary>
    public const string SecurityStamp = "sstamp";
}
