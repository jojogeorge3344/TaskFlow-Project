using FluentValidation;
using TaskFlow.Application.Users.Validators;

namespace TaskFlow.Application.PasswordReset.Validators;

/// <summary>The administrator chooses the new password for every request, so it is required and must satisfy
/// the same policy every other password change enforces (design D6).</summary>
public class PasswordResetProcessRequestValidator : AbstractValidator<PasswordResetProcessRequest>
{
    public PasswordResetProcessRequestValidator()
    {
        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("A new password is required.")
            .MinimumLength(ChangePasswordRequestValidator.MinPasswordLength)
            .MaximumLength(ChangePasswordRequestValidator.MaxPasswordLength);
    }
}
