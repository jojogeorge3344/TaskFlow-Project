using TaskFlow.Api.Dtos.Management.ProjectManagement.Validators;
using TaskFlow.Api.Dtos.Management.ProjectManagement;
using FluentValidation.TestHelper;

namespace TaskFlow.UnitTests.Management.ProjectManagement;

public class CreateProjectRequestValidatorTests
{
    private readonly CreateProjectRequestValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_fails(string name) =>
        _validator.TestValidate(new CreateProjectRequest(name, null)).ShouldHaveValidationErrorFor(request => request.Name);

    [Fact]
    public void Name_at_the_200_character_limit_passes() =>
        _validator.TestValidate(new CreateProjectRequest(new string('a', 200), null)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Name_over_the_200_character_limit_fails() =>
        _validator.TestValidate(new CreateProjectRequest(new string('a', 201), null)).ShouldHaveValidationErrorFor(request => request.Name);

    [Fact]
    public void Description_at_the_500_character_limit_passes() =>
        _validator.TestValidate(new CreateProjectRequest("Apollo", new string('d', 500))).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Description_over_the_500_character_limit_fails() =>
        _validator.TestValidate(new CreateProjectRequest("Apollo", new string('d', 501))).ShouldHaveValidationErrorFor(request => request.Description);
}

public class UpdateProjectRequestValidatorTests
{
    private readonly UpdateProjectRequestValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_fails(string name) =>
        _validator.TestValidate(new UpdateProjectRequest(name, null)).ShouldHaveValidationErrorFor(request => request.Name);

    [Fact]
    public void Name_at_the_200_character_limit_passes() =>
        _validator.TestValidate(new UpdateProjectRequest(new string('a', 200), null)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Name_over_the_200_character_limit_fails() =>
        _validator.TestValidate(new UpdateProjectRequest(new string('a', 201), null)).ShouldHaveValidationErrorFor(request => request.Name);

    [Fact]
    public void Description_at_the_500_character_limit_passes() =>
        _validator.TestValidate(new UpdateProjectRequest("Apollo", new string('d', 500))).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Description_over_the_500_character_limit_fails() =>
        _validator.TestValidate(new UpdateProjectRequest("Apollo", new string('d', 501))).ShouldHaveValidationErrorFor(request => request.Description);
}
