namespace TaskFlow.Api.Integrations.Security.Tokens;

public record RefreshTokenValue(string Token, string TokenHash, DateTime ExpiresAt);
