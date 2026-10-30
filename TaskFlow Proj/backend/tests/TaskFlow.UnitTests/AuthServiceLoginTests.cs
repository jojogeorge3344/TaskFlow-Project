using TaskFlow.Api.Dtos.Auth;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.UnitTests;

public class AuthServiceLoginTests
{
    [Fact]
    public async Task LoginAsync_WithEmail_ReturnsTokensAndRole()
    {
        await using var harness = AuthServiceHarness.Create();
        var user = harness.AddUser("admin", "admin@taskflow.com", "P@ssw0rd!", role: SystemRoles.Administrator);

        var result = await harness.Service.LoginAsync(new LoginRequest("admin@taskflow.com", "P@ssw0rd!"));

        Assert.True(result.Succeeded);
        Assert.Equal(user.Id, result.User!.Id);
        Assert.Equal("Administrator", result.User.Role);
        Assert.False(string.IsNullOrWhiteSpace(result.Tokens!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Tokens.RefreshToken));

        var reloaded = await harness.Db.Users.SingleAsync(candidate => candidate.Id == user.Id);
        Assert.NotNull(reloaded.LastLoginAt);
        Assert.Single(await harness.Db.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task LoginAsync_WithUsername_ReturnsTokens()
    {
        await using var harness = AuthServiceHarness.Create();
        harness.AddUser("manager", "manager@taskflow.com", "P@ssw0rd!", role: SystemRoles.Manager);

        var result = await harness.Service.LoginAsync(new LoginRequest("manager", "P@ssw0rd!"));

        Assert.True(result.Succeeded);
        Assert.Equal("Manager", result.User!.Role);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_Fails()
    {
        await using var harness = AuthServiceHarness.Create();
        harness.AddUser("employee", "employee@taskflow.com", "P@ssw0rd!");

        var result = await harness.Service.LoginAsync(new LoginRequest("employee", "wrong-password"));

        Assert.False(result.Succeeded);
        Assert.Null(result.Tokens);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownIdentifier_Fails()
    {
        await using var harness = AuthServiceHarness.Create();
        harness.AddUser("employee", "employee@taskflow.com", "P@ssw0rd!");

        var result = await harness.Service.LoginAsync(new LoginRequest("ghost@taskflow.com", "P@ssw0rd!"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task LoginAsync_WhenInactive_Fails()
    {
        await using var harness = AuthServiceHarness.Create();
        harness.AddUser("disabled", "disabled@taskflow.com", "P@ssw0rd!", isActive: false);

        var result = await harness.Service.LoginAsync(new LoginRequest("disabled", "P@ssw0rd!"));

        Assert.False(result.Succeeded);
    }
}
