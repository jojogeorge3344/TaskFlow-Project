using FluentValidation;
using TaskFlow.Api.Dtos.Profile.Validators;

namespace TaskFlow.Api.Dtos.Auth.PasswordRecovery.Validators;

public class AuthenticatorRecoveryRequestValidator : AbstractValidator<AuthenticatorRecoveryRequest>
{
    public AuthenticatorRecoveryRequestValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}

public class RecoverPasswordRequestValidator : AbstractValidator<RecoverPasswordRequest>
{
    public RecoverPasswordRequestValidator()
    {
        RuleFor(x => x.ResetToken).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(ChangePasswordRequestValidator.MinPasswordLength)
            .MaximumLength(ChangePasswordRequestValidator.MaxPasswordLength);

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}
