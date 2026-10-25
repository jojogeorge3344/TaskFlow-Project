using FluentValidation;

namespace TaskFlow.Application.Projects.Validators;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);
    }
}
