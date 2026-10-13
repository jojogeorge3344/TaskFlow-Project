using FluentValidation;

namespace TaskFlow.Application.Tasks.Validators;

public class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(request => request.Body)
            .NotEmpty()
            .MaximumLength(4000);
    }
}