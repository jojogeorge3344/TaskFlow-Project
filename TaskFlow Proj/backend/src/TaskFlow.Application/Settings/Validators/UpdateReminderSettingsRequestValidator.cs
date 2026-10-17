using FluentValidation;

namespace TaskFlow.Application.Settings.Validators;

public class UpdateReminderSettingsRequestValidator : AbstractValidator<UpdateReminderSettingsRequest>
{
    public const int MaxIntervalDays = 30;

    public UpdateReminderSettingsRequestValidator()
    {
        RuleFor(settings => settings.DefaultFirstDays)
            .InclusiveBetween(1, MaxIntervalDays);

        // 0 means "on the deadline day".
        RuleFor(settings => settings.DefaultSecondDays)
            .InclusiveBetween(0, MaxIntervalDays);

        RuleFor(settings => settings.DefaultSecondDays)
            .LessThan(settings => settings.DefaultFirstDays)
            .WithMessage("The first reminder must come earlier than the second reminder.")
            .When(settings => settings.DefaultFirstDays is >= 1 and <= MaxIntervalDays);

        RuleFor(settings => settings.DefaultOverdueFrequency)
            .IsInEnum();
    }
}
