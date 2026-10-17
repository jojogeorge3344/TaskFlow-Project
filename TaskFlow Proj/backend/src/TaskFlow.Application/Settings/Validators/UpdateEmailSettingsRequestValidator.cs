using System.Globalization;
using FluentValidation;

namespace TaskFlow.Application.Settings.Validators;

public class UpdateEmailSettingsRequestValidator : AbstractValidator<UpdateEmailSettingsRequest>
{
    public const int MaxHostLength = 255;
    public const int MaxAddressLength = 320;
    public const int MaxSenderNameLength = 100;

    public UpdateEmailSettingsRequestValidator()
    {
        RuleFor(settings => settings.EmailEnabled).NotNull();

        RuleFor(settings => settings.SmtpPort)
            .NotNull()
            .InclusiveBetween(1, 65535);

        RuleFor(settings => settings.SmtpSecurity)
            .NotNull()
            .IsInEnum();

        RuleFor(settings => settings.SendTime)
            .NotEmpty()
            .Must(BeAValidSendTime)
            .WithMessage("Send time must be a time in HH:mm format, for example 09:00.");

        RuleFor(settings => settings.SmtpHost).MaximumLength(MaxHostLength);
        RuleFor(settings => settings.SmtpUsername).MaximumLength(MaxAddressLength);
        RuleFor(settings => settings.SenderName).MaximumLength(MaxSenderNameLength);

        RuleFor(settings => settings.SenderAddress)
            .MaximumLength(MaxAddressLength)
            .EmailAddress()
            .When(settings => !string.IsNullOrWhiteSpace(settings.SenderAddress));

        When(settings => settings.EmailEnabled == true, () =>
        {
            RuleFor(settings => settings.SmtpHost)
                .NotEmpty()
                .WithMessage("SMTP server is required when email notifications are enabled.");
            RuleFor(settings => settings.SenderAddress)
                .NotEmpty()
                .WithMessage("Sender address is required when email notifications are enabled.");
        });
    }

    /// <summary>A 24-hour HH:mm time such as 09:00.</summary>
    public static bool BeAValidSendTime(string? value) =>
        value is { Length: 5 }
        && TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
