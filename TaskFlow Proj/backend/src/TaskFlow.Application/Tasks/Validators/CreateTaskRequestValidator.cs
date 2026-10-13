using FluentValidation;

namespace TaskFlow.Application.Tasks.Validators;

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(task => task.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(task => task.Description)
            .MaximumLength(4000)
            .When(task => task.Description is not null);

        RuleFor(task => task.AssignedToUserId)
            .GreaterThan(0)
            .WithMessage("An assigned employee is required.");

        RuleFor(task => task.Priority)
            .IsInEnum();

        RuleFor(task => task.Deadline)
            .GreaterThan(task => task.StartDate)
            .WithMessage("Deadline must be after the start date.");

        RuleFor(task => task.EstimatedHours)
            .GreaterThan(0)
            .WithMessage("Estimated hours must be a positive number.");

        RuleFor(task => task.AdditionalRecipientUserIds)
            .Must(ids => ids is null || ids.Count <= 50)
            .WithMessage("A task can have at most 50 additional recipients.");

        RuleForEach(task => task.AdditionalRecipientUserIds)
            .GreaterThan(0)
            .When(task => task.AdditionalRecipientUserIds is not null);

        RuleFor(task => task.FirstReminderDays)
            .InclusiveBetween(0, 30);

        RuleFor(task => task.SecondReminderDays)
            .InclusiveBetween(0, 30);

        RuleFor(task => task.OverdueReminderFrequency)
            .IsInEnum();
    }
}
