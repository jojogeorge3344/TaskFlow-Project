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

        // Omitted keeps the current number; an empty value clears it; anything else must be a real international number.
        RuleFor(x => x.MobileNumber)
            .Must(MobileNumberPolicy.IsValid).WithMessage(MobileNumberPolicy.InvalidMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.MobileNumber));
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

        RuleFor(x => x.MfaCode)
            .MaximumLength(32);
    }
}

