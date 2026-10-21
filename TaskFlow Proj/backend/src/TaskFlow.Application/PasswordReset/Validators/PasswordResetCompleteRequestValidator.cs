using FluentValidation;
using TaskFlow.Application.Users.Validators;

namespace TaskFlow.Application.PasswordReset.Validators;

/// <summary>Shape and policy checks for the Login Screen password update. They run before any lookup, so a field error
/// never depends on whether an account exists.</summary>
public class PasswordResetCompleteRequestValidator : AbstractValidator<PasswordResetCompleteRequest>
{
    public PasswordResetCompleteRequestValidator()
    {
        // The encrypted link is the only proof and the only way an account is named.
        RuleFor(request => request.Token)
            .NotEmpty().WithMessage("A login link is required.");

        RuleFor(request => request.NewPassword)
            .NotEmpty().WithMessage("A new password is required.")
            .MinimumLength(ChangePasswordRequestValidator.MinPasswordLength)
            .MaximumLength(ChangePasswordRequestValidator.MaxPasswordLength);
    }
}
