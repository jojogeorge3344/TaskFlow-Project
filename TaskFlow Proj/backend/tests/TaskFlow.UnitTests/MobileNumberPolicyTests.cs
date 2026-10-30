using TaskFlow.Api.Dtos.Auth;
using TaskFlow.Api.Dtos.Profile.Validators;
using TaskFlow.Api.Dtos.Profile;
using TaskFlow.Api.Common.Rules;

namespace TaskFlow.UnitTests;

public class MobileNumberPolicyTests
{
    [Theory]
    [InlineData("+919876543210")]
    [InlineData("+14155552671")]
    [InlineData("+91 98765-43210")]
    [InlineData("+91 (98765) 43210")]
    public void Valid_numbers_are_accepted(string number) => Assert.True(MobileNumberPolicy.IsValid(number));

    [Theory]
    [InlineData("9876543210")]
    [InlineData("+0123456789")]
    [InlineData("+12345")]
    [InlineData("+1234567890123456")]
    [InlineData("+91abc4567890")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_numbers_are_rejected(string? number) => Assert.False(MobileNumberPolicy.IsValid(number));

    [Fact]
    public void Normalize_strips_formatting_and_blank_means_none()
    {
        Assert.Equal("+919876543210", MobileNumberPolicy.Normalize(" +91 (98765)-43210 "));
        Assert.Null(MobileNumberPolicy.Normalize("   "));
        Assert.Null(MobileNumberPolicy.Normalize(null));
    }

    [Fact]
    public void The_profile_validator_allows_omitted_or_empty_and_rejects_a_malformed_number()
    {
        var validator = new UpdateOwnProfileRequestValidator();

        Assert.True(validator.Validate(new UpdateOwnProfileRequest("Jo", "jo@taskflow.com")).IsValid);
        Assert.True(validator.Validate(new UpdateOwnProfileRequest("Jo", "jo@taskflow.com", "")).IsValid);
        Assert.True(validator.Validate(new UpdateOwnProfileRequest("Jo", "jo@taskflow.com", "+919876543210")).IsValid);
        Assert.False(validator.Validate(new UpdateOwnProfileRequest("Jo", "jo@taskflow.com", "12345")).IsValid);
    }
}

public class MfaRequiredFlagTests
{
    [Fact]
    public async Task Sign_in_reports_that_setup_is_required_for_a_user_without_mfa_and_not_once_it_is_on()
    {
        await using var harness = AuthServiceHarness.Create();
        var user = harness.AddUser("gate-flag", "gate-flag@taskflow.com", "P@ssw0rd!");

        var before = await harness.Service.LoginAsync(new LoginRequest("gate-flag", "P@ssw0rd!"));
        Assert.True(before.Succeeded);
        Assert.True(before.User!.MfaSetupRequired);

        user.MfaEnabledAt = DateTime.UtcNow;
        await harness.Db.SaveChangesAsync();
        var after = await harness.Service.LoginAsync(new LoginRequest("gate-flag", "P@ssw0rd!"));
        Assert.True(after.MfaRequired);
    }
}
