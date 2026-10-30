using FluentValidation;

namespace TaskFlow.Application.Tasks.Validators;

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(task => task.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(task => task.Description)
            .MaximumLength(4000)
            .When(task => task.Description is not null);

        RuleFor(task => task.Deadline)
            .GreaterThan(task => task.StartDate)
            .WithMessage("Deadline must be after the start date.");

        RuleFor(task => task.EstimatedHours)
            .GreaterThan(0)
            .WithMessage("Estimated hours must be a positive number.");
    }
}