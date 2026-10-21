using TaskFlow.Application.PasswordReset;
using TaskFlow.Application.PasswordReset.Validators;

namespace TaskFlow.UnitTests;

/// <summary>The identifier a visitor submits must be present and length-bounded (measured after
/// trimming); a submission that fails here never reaches the request table.</summary>
public class PasswordResetSubmissionValidatorTests
{
    private readonly PasswordResetSubmissionValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyIdentifierIsRejectedWithAFieldError(string? identifier)
    {
        var result = _validator.Validate(new PasswordResetSubmission(identifier));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("Identifier", error.PropertyName);
        Assert.NotNull(error.ErrorMessage);
    }

    [Fact]
    public void OversizedIdentifierIsRejectedWithAFieldError()
    {
        var identifier = new string('a', PasswordResetSubmissionValidator.MaxIdentifierLength + 1);

        var result = _validator.Validate(new PasswordResetSubmission(identifier));

        Assert.False(result.IsValid);
        Assert.All(result.Errors, error => Assert.Equal("Identifier", error.PropertyName));
    }

    [Fact]
    public void IdentifierAtTheLimitAndSurroundingWhitespaceIsAccepted()
    {
        var atLimit = new string('a', PasswordResetSubmissionValidator.MaxIdentifierLength);

        Assert.True(_validator.Validate(new PasswordResetSubmission(atLimit)).IsValid);
        Assert.True(_validator.Validate(new PasswordResetSubmission($" {atLimit} ")).IsValid);
        Assert.True(_validator.Validate(new PasswordResetSubmission(" user@example.com ")).IsValid);
    }
}
