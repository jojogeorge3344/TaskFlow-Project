using System.IdentityModel.Tokens.Jwt;
using TaskFlow.Domain.Security;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Authentication;

namespace TaskFlow.UnitTests;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(out JwtOptions options)
    {
        options = new JwtOptions
        {
            Issuer = "TaskFlow",
            Key = new string('k', 64),
            AccessTokenMinutes = 30,
            RefreshTokenDays = 7
        };

        return new JwtTokenService(Options.Create(options), TimeProvider.System);
    }

    [Fact]
    public void CreateAccessToken_IssuesTokenCarryingUserClaims()
    {
        var service = CreateService(out var options);
        var user = new User
        {
            Id = 42,
            Username = "admin",
            FullName = "Admin User",
            RoleId = SystemRoles.AdministratorId,
            Role = new Role { Id = SystemRoles.AdministratorId, Name = SystemRoles.Administrator }
        };

        var accessToken = service.CreateAccessToken(user);

        var principal = new JwtSecurityTokenHandler().ValidateToken(
            accessToken.Token,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = options.Issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key))
            },
            out _);

        Assert.Equal("42", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Administrator", principal.FindFirst(ClaimTypes.Role)?.Value);
        Assert.Equal("Admin User", principal.FindFirst(ClaimTypes.Name)?.Value);
    }

    [Fact]
    public void CreateRefreshToken_ProducesDistinctRotatableTokens()
    {
        var service = CreateService(out _);

        var first = service.CreateRefreshToken();
        var second = service.CreateRefreshToken();

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.TokenHash, second.TokenHash);
        Assert.NotEqual(first.Token, first.TokenHash);
        Assert.Equal(first.TokenHash, service.HashRefreshToken(first.Token));
        Assert.True(first.ExpiresAt > DateTime.UtcNow);
    }
}
