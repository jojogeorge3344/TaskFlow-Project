using FluentValidation.TestHelper;
using TaskFlow.Application.Users;
using TaskFlow.Application.Users.Validators;

namespace TaskFlow.UnitTests;

public class UpdateOwnProfileRequestValidatorTests
{
    private readonly UpdateOwnProfileRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        _validator.TestValidate(new UpdateOwnProfileRequest("Jo Jo", "jo@taskflow.local"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Blank_name_is_invalid()
    {
        _validator.TestValidate(new UpdateOwnProfileRequest("   ", "jo@taskflow.local"))
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
        _validator.TestValidate(new ChangePasswordRequest("P@ssw0rd!", "N3wP@ss!"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Blank_current_password_is_invalid()
    {
        _validator.TestValidate(new ChangePasswordRequest("  ", "N3wP@ss!"))
            .ShouldHaveValidationErrorFor(x => x.CurrentPassword);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void Short_new_password_is_invalid(string? newPassword)
    {
        _validator.TestValidate(new ChangePasswordRequest("P@ssw0rd!", newPassword))
            .ShouldHaveValidationErrorFor(x => x.NewPassword);
    }
}
