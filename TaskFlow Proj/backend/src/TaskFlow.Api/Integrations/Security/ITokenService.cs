using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Integrations.Security;

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    RefreshTokenValue CreateRefreshToken();

    string HashRefreshToken(string refreshToken);
}

public record AccessToken(string Token, DateTime ExpiresAt);

public record RefreshTokenValue(string Token, string TokenHash, DateTime ExpiresAt);
