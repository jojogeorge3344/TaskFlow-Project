namespace TaskFlow.Api.Config;

/// <summary>Settings for authenticator-app MFA. Nothing here is secret.</summary>
public class MfaOptions
{
    public const string SectionName = "Mfa";

    /// <summary>The name an authenticator app shows beside the account.</summary>
    public string Issuer { get; set; } = "TaskFlow";

    /// <summary>When true (the default) nobody can use the app until they have set up an authenticator app. Switch it off
    /// (<c>Mfa__Required=false</c>) only as a break-glass measure, for example if every administrator is locked out.</summary>
    public bool Required { get; set; } = true;

    /// <summary>Consecutive wrong codes before verification is locked for the account.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 5;

    public int BackupCodeCount { get; set; } = 10;

    /// <summary>How long the sign-in challenge issued after a correct password stays valid.</summary>
    public int ChallengeMinutes { get; set; } = 5;
}
