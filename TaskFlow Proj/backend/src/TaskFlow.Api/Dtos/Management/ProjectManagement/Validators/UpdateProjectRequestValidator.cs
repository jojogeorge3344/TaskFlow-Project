using TaskFlow.Api.Dtos.Management.ProjectManagement;
using FluentValidation;

namespace TaskFlow.Api.Dtos.Management.ProjectManagement.Validators;

public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);
    }
}
