using FluentValidation;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Settings.Validators;

public class UpdateApplicationSettingsRequestValidator : AbstractValidator<UpdateApplicationSettingsRequest>
{
    public UpdateApplicationSettingsRequestValidator()
    {
        RuleFor(settings => settings.Name)
            .NotEmpty()
            .WithMessage("Application name cannot be blank.")
            .MaximumLength(100);

        RuleFor(settings => settings.Timezone)
            .NotEmpty()
            .WithMessage("Timezone cannot be blank.")
            .Must(BeKnownTimezone)
            .WithMessage("Unknown timezone.");

        RuleFor(settings => settings.DefaultPriority)
            .NotNull()
            .IsInEnum();

        RuleFor(settings => settings.InAppNotificationsEnabled)
            .NotNull();
    }

    /// <summary>Resolves the id against the host's timezone database (Windows or IANA ids, whichever
    /// the running OS recognises). Unknown ids are rejected with a clear validation message.</summary>
    private static bool BeKnownTimezone(string? timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return true;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }
}
