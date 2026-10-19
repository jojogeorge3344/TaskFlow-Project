using FluentValidation;

namespace TaskFlow.Application.Users.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty()
            .Must(EmailPolicy.IsValid).WithMessage(EmailPolicy.InvalidMessage)
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .Must(password => !string.IsNullOrWhiteSpace(password))
            .WithMessage("Password cannot be blank.")
            .MinimumLength(6)
            .When(x => !string.IsNullOrEmpty(x.Password));
    }
}
