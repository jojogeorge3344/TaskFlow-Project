using FluentValidation;

namespace TaskFlow.Application.Settings.Validators;

public class UpdateReminderSettingsRequestValidator : AbstractValidator<UpdateReminderSettingsRequest>
{
    public const int MaxIntervalDays = 30;

    public UpdateReminderSettingsRequestValidator()
    {
        RuleFor(settings => settings.Enabled).NotNull();
        RuleFor(settings => settings.DeadlineDayEnabled).NotNull();
        RuleFor(settings => settings.MailEnabled).NotNull();

        RuleFor(settings => settings.DefaultFirstDays)
            .NotNull()
            .InclusiveBetween(1, MaxIntervalDays);

        // 0 means "on the deadline day".
        RuleFor(settings => settings.DefaultSecondDays)
            .NotNull()
            .InclusiveBetween(0, MaxIntervalDays);

        RuleFor(settings => settings.DefaultSecondDays)
            .LessThan(settings => settings.DefaultFirstDays)
            .WithMessage("The first reminder must come earlier than the second reminder.")
            .When(settings => settings.DefaultFirstDays is >= 1 and <= MaxIntervalDays && settings.DefaultSecondDays is not null);

        RuleFor(settings => settings.DefaultOverdueFrequency)
            .NotNull()
            .IsInEnum();
    }
}
