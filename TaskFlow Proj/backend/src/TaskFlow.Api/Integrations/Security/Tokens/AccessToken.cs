namespace TaskFlow.Api.Integrations.Security.Tokens;

public record AccessToken(string Token, DateTime ExpiresAt);
