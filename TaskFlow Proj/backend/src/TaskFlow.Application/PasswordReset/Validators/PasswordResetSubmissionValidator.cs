using FluentValidation;

namespace TaskFlow.Application.PasswordReset.Validators;

/// <summary>Bounds the identifier a visitor types into the Forgot Password dialog: required,
/// measured after trimming, and length-bounded. Normalization (trimming) happens in the service;
/// this validator only decides whether the submission is well-formed.</summary>
public class PasswordResetSubmissionValidator : AbstractValidator<PasswordResetSubmission>
{
    public const int MaxIdentifierLength = 256;

    public PasswordResetSubmissionValidator()
    {
        RuleFor(request => request.Identifier)
            .NotEmpty().WithMessage("An email address or username is required.")
            // Null-safe: a missing identifier is already reported by NotEmpty above.
            .Must(identifier => (identifier ?? string.Empty).Trim().Length <= MaxIdentifierLength)
            .WithMessage($"The email address or username must be at most {MaxIdentifierLength} characters.");
    }
}
