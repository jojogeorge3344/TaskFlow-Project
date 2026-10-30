using FluentValidation;

namespace TaskFlow.Application.Tasks.Validators;

public class UpdateTaskStatusRequestValidator : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .IsInEnum();
    }
}