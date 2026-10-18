using FluentValidation.TestHelper;
using TaskFlow.Application.Settings;
using TaskFlow.Application.Settings.Validators;
using TaskFlow.Domain.Enums;

namespace TaskFlow.UnitTests;

public class ApplicationSettingsValidatorTests
{
    private readonly UpdateApplicationSettingsRequestValidator _validator = new();

    private static UpdateApplicationSettingsRequest Valid() =>
        new("TaskFlow", "UTC", TaskPriority.Medium, true);

    [Fact]
    public void Defaults_are_valid()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Blank_name_is_invalid()
    {
        _validator.TestValidate(Valid() with { Name = "   " })
            .ShouldHaveValidationErrorFor(settings => settings.Name)
            .WithErrorMessage("Application name cannot be blank.");
    }

    [Fact]
    public void Unknown_timezone_is_invalid()
    {
        _validator.TestValidate(Valid() with { Timezone = "Mars/Olympus_Mons" })
            .ShouldHaveValidationErrorFor(settings => settings.Timezone)
            .WithErrorMessage("Unknown timezone.");
    }

    [Fact]
    public void Missing_fields_are_invalid_instead_of_defaulting()
    {
        var result = _validator.TestValidate(new UpdateApplicationSettingsRequest(null, null, null, null));

        result.ShouldHaveValidationErrorFor(settings => settings.Name);
        result.ShouldHaveValidationErrorFor(settings => settings.Timezone);
        result.ShouldHaveValidationErrorFor(settings => settings.DefaultPriority);
        result.ShouldHaveValidationErrorFor(settings => settings.InAppNotificationsEnabled);
    }

    [Fact]
    public void Priority_outside_the_enum_is_invalid()
    {
        _validator.TestValidate(Valid() with { DefaultPriority = (TaskPriority)99 })
            .ShouldHaveValidationErrorFor(settings => settings.DefaultPriority);
    }
}
