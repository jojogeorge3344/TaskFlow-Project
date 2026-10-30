using FluentValidation.TestHelper;
using TaskFlow.Application.Settings;
using TaskFlow.Application.Settings.Validators;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.UnitTests;

public class ReminderSettingsValidatorTests
{
    private readonly UpdateReminderSettingsRequestValidator _validator = new();

    private static UpdateReminderSettingsRequest Valid() =>
        new(true, true, 2, 1, ReminderFrequency.Daily, false);

    [Fact]
    public void Defaults_are_valid()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Second_interval_of_zero_is_valid()
    {
        _validator.TestValidate(Valid() with { DefaultSecondDays = 0 }).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(45)]
    public void First_interval_out_of_range_is_invalid(int days)
    {
        _validator.TestValidate(Valid() with { DefaultFirstDays = days })
            .ShouldHaveValidationErrorFor(settings => settings.DefaultFirstDays);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public void Second_interval_out_of_range_is_invalid(int days)
    {
        _validator.TestValidate(Valid() with { DefaultFirstDays = 30, DefaultSecondDays = days })
            .ShouldHaveValidationErrorFor(settings => settings.DefaultSecondDays);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    public void Second_interval_not_before_first_is_invalid(int first, int second)
    {
        _validator.TestValidate(Valid() with { DefaultFirstDays = first, DefaultSecondDays = second })
            .ShouldHaveValidationErrorFor(settings => settings.DefaultSecondDays)
            .WithErrorMessage("The first reminder must come earlier than the second reminder.");
    }

    [Fact]
    public void Unknown_overdue_frequency_is_invalid()
    {
        _validator.TestValidate(Valid() with { DefaultOverdueFrequency = (ReminderFrequency)99 })
            .ShouldHaveValidationErrorFor(settings => settings.DefaultOverdueFrequency);
    }

    [Fact]
    public void Missing_fields_are_invalid_instead_of_defaulting()
    {
        var result = _validator.TestValidate(new UpdateReminderSettingsRequest(null, null, 3, 1, null, null));

        result.ShouldHaveValidationErrorFor(settings => settings.Enabled);
        result.ShouldHaveValidationErrorFor(settings => settings.DeadlineDayEnabled);
        result.ShouldHaveValidationErrorFor(settings => settings.DefaultOverdueFrequency);
        result.ShouldHaveValidationErrorFor(settings => settings.MailEnabled);
    }

    [Fact]
    public void Missing_intervals_are_invalid()
    {
        var result = _validator.TestValidate(Valid() with { DefaultFirstDays = null, DefaultSecondDays = null });

        result.ShouldHaveValidationErrorFor(settings => settings.DefaultFirstDays);
        result.ShouldHaveValidationErrorFor(settings => settings.DefaultSecondDays);
    }
}
