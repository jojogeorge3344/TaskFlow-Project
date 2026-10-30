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

        // No password rule: an update ignores the password field (people change their own password from their profile).
    }
}
