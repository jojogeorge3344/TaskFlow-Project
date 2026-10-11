namespace TaskFlow.Application.Auth;

public sealed record AuthResult(AuthTokens? Tokens, UserDto? User)
{
    public bool Succeeded => Tokens is not null && User is not null;

    public static AuthResult Success(AuthTokens tokens, UserDto user) => new(tokens, user);

    public static AuthResult Failure() => new(null, null);
}
