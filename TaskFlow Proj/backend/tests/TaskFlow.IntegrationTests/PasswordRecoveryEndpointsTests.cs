using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskFlow.IntegrationTests;

/// <summary>The recovery reset step, and proof that the text-message routes are gone.</summary>
public class PasswordRecoveryEndpointsTests : IClassFixture<TaskFlowApiFactory>
{
    private const string Recover = "/api/v1/auth/password-recovery/authenticator/verify";
    private const string Reset = "/api/v1/auth/password-recovery/reset";
    private const string NewPassword = "N3wP@ssw0rd!";

    private readonly MfaTestHarness _mfa;

    public PasswordRecoveryEndpointsTests(TaskFlowApiFactory factory)
    {
        _mfa = new MfaTestHarness(factory);
    }

    [Theory]
    [InlineData("POST", "/api/v1/auth/password-otp/request")]
    [InlineData("POST", "/api/v1/auth/password-otp/verify")]
    [InlineData("POST", "/api/v1/auth/password-otp/reset")]
    public async Task The_text_message_routes_no_longer_exist(string method, string url)
    {
        var client = _mfa.Factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = JsonContent.Create(new { mobileNumber = "+919876543210", code = "123456" })
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reset_without_a_valid_token_is_refused()
    {
        var client = _mfa.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(Reset, new { resetToken = "forged", newPassword = NewPassword, confirmPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_rejected_password_leaves_the_token_usable()
    {
        var client = _mfa.Factory.CreateClient();
        var accessToken = await _mfa.SeedAndLoginAsync(client, "rec-weak");
        var (secret, _) = await _mfa.EnrolAsync(client, accessToken);
        var verify = await client.PostAsJsonAsync(Recover, new { identifier = "rec-weak", code = _mfa.NextCode(secret) });
        using var document = JsonDocument.Parse(await verify.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("resetToken").GetString();

        var mismatch = await client.PostAsJsonAsync(Reset, new { resetToken = token, newPassword = NewPassword, confirmPassword = "different" });
        var weak = await client.PostAsJsonAsync(Reset, new { resetToken = token, newPassword = "123", confirmPassword = "123" });
        var good = await client.PostAsJsonAsync(Reset, new { resetToken = token, newPassword = NewPassword, confirmPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, good.StatusCode);
    }
}
