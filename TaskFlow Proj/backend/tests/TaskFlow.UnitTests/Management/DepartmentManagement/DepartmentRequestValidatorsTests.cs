using TaskFlow.Api.Dtos.Management.DepartmentManagement.Validators;
using TaskFlow.Api.Dtos.Management.DepartmentManagement;
using FluentValidation.TestHelper;

namespace TaskFlow.UnitTests.Management.DepartmentManagement;

public class CreateDepartmentRequestValidatorTests
{
    private readonly CreateDepartmentRequestValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_fails(string name) =>
        _validator.TestValidate(new CreateDepartmentRequest(name, null)).ShouldHaveValidationErrorFor(request => request.Name);

    [Fact]
    public void Name_at_the_200_character_limit_passes() =>
        _validator.TestValidate(new CreateDepartmentRequest(new string('a', 200), null)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Name_over_the_200_character_limit_fails() =>
        _validator.TestValidate(new CreateDepartmentRequest(new string('a', 201), null)).ShouldHaveValidationErrorFor(request => request.Name);

    [Fact]
    public void Description_at_the_500_character_limit_passes() =>
        _validator.TestValidate(new CreateDepartmentRequest("Operations", new string('d', 500))).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Description_over_the_500_character_limit_fails() =>
        _validator.TestValidate(new CreateDepartmentRequest("Operations", new string('d', 501))).ShouldHaveValidationErrorFor(request => request.Description);
}

public class UpdateDepartmentRequestValidatorTests
{
    private readonly UpdateDepartmentRequestValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_fails(string name) =>
        _validator.TestValidate(new UpdateDepartmentRequest(name, null)).ShouldHaveValidationErrorFor(request => request.Name);

    [Fact]
    public void Name_at_the_200_character_limit_passes() =>
        _validator.TestValidate(new UpdateDepartmentRequest(new string('a', 200), null)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Name_over_the_200_character_limit_fails() =>
        _validator.TestValidate(new UpdateDepartmentRequest(new string('a', 201), null)).ShouldHaveValidationErrorFor(request => request.Name);

    [Fact]
    public void Description_at_the_500_character_limit_passes() =>
        _validator.TestValidate(new UpdateDepartmentRequest("Operations", new string('d', 500))).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Description_over_the_500_character_limit_fails() =>
        _validator.TestValidate(new UpdateDepartmentRequest("Operations", new string('d', 501))).ShouldHaveValidationErrorFor(request => request.Description);
}
