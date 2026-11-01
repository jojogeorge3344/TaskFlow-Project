using Microsoft.AspNetCore.Http;

namespace TaskFlow.Api.Common.Security;

public static class RefreshTokenCookie
{
    public const string Name = "taskflow_refresh";

    public const string Path = "/api/v1/auth";

    public static CookieOptions CreateOptions(DateTime expiresAt)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = expiresAt
        };
    }
}
