using FluentValidation;

namespace TaskFlow.Application.Users.Validators;

public class UpdateOwnProfileRequestValidator : AbstractValidator<UpdateOwnProfileRequest>
{
    public UpdateOwnProfileRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .Must(EmailPolicy.IsValid).WithMessage(EmailPolicy.InvalidMessage)
            .MaximumLength(256);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    // Mirrors LoginRequestValidator: a password outside this range can never sign in.
    public const int MinPasswordLength = 6;
    public const int MaxPasswordLength = 128;

    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(MinPasswordLength)
            .MaximumLength(MaxPasswordLength);
    }
}
