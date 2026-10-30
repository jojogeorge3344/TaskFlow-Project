using TaskFlow.Api.Dtos.Auth;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.UnitTests;

public class AuthServiceRefreshAndLogoutTests
{
    [Fact]
    public async Task RefreshAsync_RotatesToken_AndRejectsTheOldOne()
    {
        await using var harness = AuthServiceHarness.Create();
        harness.AddUser("employee", "employee@taskflow.com", "P@ssw0rd!");

        var login = await harness.Service.LoginAsync(new LoginRequest("employee", "P@ssw0rd!"));
        var originalRefreshToken = login.Tokens!.RefreshToken;

        var refreshed = await harness.Service.RefreshAsync(originalRefreshToken);

        Assert.True(refreshed.Succeeded);
        Assert.NotEqual(originalRefreshToken, refreshed.Tokens!.RefreshToken);

        var replay = await harness.Service.RefreshAsync(originalRefreshToken);
        Assert.False(replay.Succeeded);

        var secondRotation = await harness.Service.RefreshAsync(refreshed.Tokens.RefreshToken);
        Assert.True(secondRotation.Succeeded);
    }

    [Fact]
    public async Task RefreshAsync_AfterLogout_Fails()
    {
        await using var harness = AuthServiceHarness.Create();
        harness.AddUser("employee", "employee@taskflow.com", "P@ssw0rd!");

        var login = await harness.Service.LoginAsync(new LoginRequest("employee", "P@ssw0rd!"));
        var refreshToken = login.Tokens!.RefreshToken;

        await harness.Service.LogoutAsync(refreshToken);

        var refreshAfterLogout = await harness.Service.RefreshAsync(refreshToken);
        Assert.False(refreshAfterLogout.Succeeded);
    }

    [Fact]
    public async Task RefreshAsync_WithExpiredToken_Fails()
    {
        await using var harness = AuthServiceHarness.Create();
        harness.AddUser("employee", "employee@taskflow.com", "P@ssw0rd!");

        var login = await harness.Service.LoginAsync(new LoginRequest("employee", "P@ssw0rd!"));

        var stored = await harness.Db.RefreshTokens.SingleAsync();
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await harness.Db.SaveChangesAsync();

        var result = await harness.Service.RefreshAsync(login.Tokens!.RefreshToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RefreshAsync_WithUnknownToken_Fails()
    {
        await using var harness = AuthServiceHarness.Create();

        var result = await harness.Service.RefreshAsync("not-a-real-token");

        Assert.False(result.Succeeded);
    }
}
