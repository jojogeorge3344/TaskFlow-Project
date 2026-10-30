using TaskFlow.Api.Dtos.Profile.Validators;
using TaskFlow.Api.Dtos.Profile;
using FluentValidation.TestHelper;

namespace TaskFlow.UnitTests;

public class UpdateOwnProfileRequestValidatorTests
{
    private readonly UpdateOwnProfileRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        _validator.TestValidate(new UpdateOwnProfileRequest("Jo Jo", "jo@taskflow.com"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Blank_name_is_invalid()
    {
        _validator.TestValidate(new UpdateOwnProfileRequest("   ", "jo@taskflow.com"))
            .ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void Malformed_email_is_invalid()
    {
        _validator.TestValidate(new UpdateOwnProfileRequest("Jo Jo", "not-an-email"))
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Missing_fields_are_invalid()
    {
        var result = _validator.TestValidate(new UpdateOwnProfileRequest(null, null));

        result.ShouldHaveValidationErrorFor(x => x.FullName);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}

public class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        _validator.TestValidate(new ChangePasswordRequest("N3wP@ss!"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void Short_new_password_is_invalid(string? newPassword)
    {
        _validator.TestValidate(new ChangePasswordRequest(newPassword))
            .ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void New_password_longer_than_login_accepts_is_invalid()
    {
        var tooLong = new string('x', ChangePasswordRequestValidator.MaxPasswordLength + 1);

        _validator.TestValidate(new ChangePasswordRequest(tooLong))
            .ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void New_password_at_the_maximum_length_passes()
    {
        var atLimit = new string('x', ChangePasswordRequestValidator.MaxPasswordLength);

        _validator.TestValidate(new ChangePasswordRequest(atLimit))
            .ShouldNotHaveAnyValidationErrors();
    }
}
