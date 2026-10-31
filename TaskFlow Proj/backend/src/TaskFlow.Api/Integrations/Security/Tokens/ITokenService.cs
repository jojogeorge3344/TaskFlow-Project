using TaskFlow.Api.Models.Entities;

namespace TaskFlow.Api.Integrations.Security.Tokens;

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    RefreshTokenValue CreateRefreshToken();

    string HashRefreshToken(string refreshToken);
}
