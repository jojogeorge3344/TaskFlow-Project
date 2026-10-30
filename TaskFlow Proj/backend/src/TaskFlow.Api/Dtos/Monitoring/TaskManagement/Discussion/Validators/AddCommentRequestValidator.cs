using TaskFlow.Api.Dtos.Monitoring.TaskManagement.Discussion;
using FluentValidation;

namespace TaskFlow.Api.Dtos.Monitoring.TaskManagement.Discussion.Validators;

public class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(request => request.Body)
            .NotEmpty()
            .MaximumLength(4000);
    }
}