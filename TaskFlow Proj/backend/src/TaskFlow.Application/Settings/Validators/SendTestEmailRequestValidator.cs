using FluentValidation;

namespace TaskFlow.Application.Settings.Validators;

public class SendTestEmailRequestValidator : AbstractValidator<SendTestEmailRequest>
{
    public SendTestEmailRequestValidator()
    {
        RuleFor(request => request.ToAddress)
            .MaximumLength(UpdateEmailSettingsRequestValidator.MaxAddressLength)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.ToAddress));
    }
}
