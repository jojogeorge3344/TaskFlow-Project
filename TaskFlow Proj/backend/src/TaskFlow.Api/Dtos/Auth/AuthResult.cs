namespace TaskFlow.Api.Dtos.Auth;

/// <summary>The outcome of a sign-in step: a session, a request for the second (MFA) step, or a failure.</summary>
public sealed record AuthResult(AuthTokens? Tokens, UserDto? User, string? MfaChallengeToken = null)
{
    public bool Succeeded => Tokens is not null && User is not null;

    /// <summary>The password was right but the account has MFA on, so no session exists yet.</summary>
    public bool MfaRequired => MfaChallengeToken is not null;

    public static AuthResult Success(AuthTokens tokens, UserDto user) => new(tokens, user);

    public static AuthResult RequiresMfa(string challengeToken) => new(null, null, challengeToken);

    public static AuthResult Failure() => new(null, null);
}

/// <summary>The second sign-in step. The outcome lets the caller tell a wrong code from a locked account.</summary>
public sealed record MfaSignInResult(AuthResult Auth, TaskFlow.Api.Dtos.Auth.Mfa.MfaVerifyOutcome Outcome);
